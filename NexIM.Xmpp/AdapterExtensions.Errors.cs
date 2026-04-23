using System.ComponentModel;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp;

using static StatusCode;

partial class AdapterExtensions
{
    public static XmppStanzaException? ToStanzaException(this StatusCode code)
    {
        // Ignore specific
        return (StatusCode)((int)code / 10 * 10) switch {
            Received => null,
            Success => null,
            Unavailable => XmppStanzaException.ServiceUnavailable(),
            NotFound => XmppStanzaException.ItemNotFound(),
            ServerNotFound => XmppStanzaException.RemoteServerNotFound(),
            RecipientUnreachable => XmppStanzaException.RecipientUnavailable(),
            ServerUnreachable => XmppStanzaException.RemoteServerTimeout(),
            RecipientGone => XmppStanzaException.Gone(null),
            RecipientChanged => XmppStanzaException.Redirect(null),
            Unauthorized => XmppStanzaException.Forbidden(),
            RegistrationRequired => XmppStanzaException.RegistrationRequired(),
            AuthenticationRequired => XmppStanzaException.NotAuthorized(),
            SubscriptionRequired => XmppStanzaException.SubscriptionRequired(),
            AlreadyExists => XmppStanzaException.Conflict(),
            Prohibited => XmppStanzaException.NotAllowed(),
            InvalidRequest => XmppStanzaException.BadRequest(),
            UnrecognizedRequest => XmppStanzaException.FeatureNotImplemented(ErrorType.Cancel),
            UnexpectedRequest => XmppStanzaException.UnexpectedRequest(ErrorType.Modify),
            InvalidParameter => XmppStanzaException.NotAcceptable(),
            UnrecognizedParameter => XmppStanzaException.FeatureNotImplemented(ErrorType.Modify),
            InvalidAddress => XmppStanzaException.JidMalformed(),
            InternalError => XmppStanzaException.InternalServerError(),
            Blocked => XmppStanzaException.PolicyViolation(ErrorType.Modify),
            ImproperTime => XmppStanzaException.PolicyViolation(ErrorType.Wait),
            NotReady => XmppStanzaException.UnexpectedRequest(ErrorType.Wait),
            InsufficientResources => XmppStanzaException.ResourceConstraint(),
            _ => throw new InvalidEnumArgumentException(nameof(code), (int)code, typeof(StatusCode))
        };
    }

    public static StatusCode ToErrorCode(this XmppStanzaException exception)
    {
        var handler = new ErrorHandler(exception.Type);
        var task = exception.Output(handler);
        if(!task.IsCompletedSuccessfully)
        {
            // Should not happen since handler calls are synchronous
            task.AsTask().GetAwaiter().GetResult();
        }
        return handler.Code ?? UnknownError;
    }

    public static RecommendedErrorAction ToRecommendedAction(this ErrorType errorType)
    {
        return errorType switch {
            ErrorType.Auth => RecommendedErrorAction.Authenticate,
            ErrorType.Cancel => RecommendedErrorAction.Abandon,
            ErrorType.Continue => RecommendedErrorAction.Proceed,
            ErrorType.Modify => RecommendedErrorAction.Modify,
            ErrorType.Wait => RecommendedErrorAction.TryAgain,
            _ => throw new InvalidEnumArgumentException(nameof(errorType), (int)errorType, typeof(ErrorType))
        };
    }

    public static ErrorType ToErrorType(this RecommendedErrorAction action)
    {
        return action switch {
            RecommendedErrorAction.Abandon => ErrorType.Cancel,
            RecommendedErrorAction.Authenticate => ErrorType.Auth,
            RecommendedErrorAction.Modify => ErrorType.Modify,
            RecommendedErrorAction.Proceed => ErrorType.Continue,
            RecommendedErrorAction.TryAgain => ErrorType.Wait,
            _ => throw new InvalidEnumArgumentException(nameof(action), (int)action, typeof(RecommendedErrorAction))
        };
    }

    sealed class ErrorHandler(ErrorType? type) : BaseStanzaErrorHandler<EmptyPayloadHandlerContext>
    {
        public StatusCode? Code { get; private set; }

        private ValueTask Detect(StatusCode code)
        {
            Code = code;
            return default;
        }

        protected override ValueTask OnBadRequest() => Detect(InvalidRequest);
        protected override ValueTask OnConflict() => Detect(AlreadyExists);
        protected override ValueTask OnFeatureNotImplemented() => Detect(type == ErrorType.Modify ? UnrecognizedParameter : UnrecognizedRequest);
        protected override ValueTask OnForbidden() => Detect(Unauthorized);
        protected override ValueTask OnGone(string? newAddress) => Detect(RecipientGone);
        protected override ValueTask OnInternalServerError() => Detect(InternalError);
        protected override ValueTask OnItemNotFound() => Detect(NotFound);
        protected override ValueTask OnJidMalformed() => Detect(InvalidAddress);
        protected override ValueTask OnNotAcceptable() => Detect(InvalidParameter);
        protected override ValueTask OnNotAllowed() => Detect(Prohibited);
        protected override ValueTask OnNotAuthorized() => Detect(AuthenticationRequired);
        protected override ValueTask OnPolicyViolation() => Detect(type == ErrorType.Wait ? ImproperTime : Blocked);
        protected override ValueTask OnPaymentRequired() => Detect(InsufficientResources); // Close enough
        protected override ValueTask OnRecipientUnavailable() => Detect(RecipientUnreachable);
        protected override ValueTask OnRedirect(string? alternateAddress) => Detect(RecipientChanged);
        protected override ValueTask OnRegistrationRequired() => Detect(RegistrationRequired);
        protected override ValueTask OnRemoteServerNotFound() => Detect(ServerNotFound);
        protected override ValueTask OnRemoteServerTimeout() => Detect(ServerUnreachable);
        protected override ValueTask OnResourceConstraint() => Detect(InsufficientResources);
        protected override ValueTask OnServiceUnavailable() => Detect(Unavailable);
        protected override ValueTask OnSubscriptionRequired() => Detect(SubscriptionRequired);
        protected override ValueTask OnUndefinedCondition() => Detect(UnknownError);
        protected override ValueTask OnUnexpectedRequest() => Detect(type == ErrorType.Wait ? NotReady : UnexpectedRequest);

        protected override ValueTask OnText(LanguageTaggedString? text) => default;
        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => default;
        public override ValueTask DisposeAsync() => default;
    }
}
