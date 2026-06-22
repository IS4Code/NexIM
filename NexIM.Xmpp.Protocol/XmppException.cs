using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

using static ErrorType;

public abstract class XmppException : ApplicationException
{
    // May be absent if no error message was provide during construction.
    public override string? Message { get; }

    internal XmppException()
    {

    }

    public XmppException(string? message) : base(message)
    {
        Message = message;
    }

    public XmppException(string? message, Exception? innerException) : base(message, innerException)
    {
        Message = message;
    }
}

public abstract class XmppException<THandler> : XmppException where THandler : IPayloadHandler
{
    readonly Func<THandler, ValueTask> details;

    public LocalizedString? LocalizedMessage { get; }

    public XmppException(Func<THandler, ValueTask> details)
    {
        this.details = details;
    }

    public XmppException(string? message, Func<THandler, ValueTask> details) : base(message)
    {
        this.details = details;

        if(message != null)
        {
            LocalizedMessage = new(new LanguageTaggedString(message));
        }
    }

    public XmppException(string? message, Func<THandler, ValueTask> details, Exception? innerException) : base(message, innerException)
    {
        this.details = details;

        if(message != null)
        {
            LocalizedMessage = new(new LanguageTaggedString(message));
        }
    }

    public XmppException(LocalizedString? message, Func<THandler, ValueTask> details) : base(message?.ToString())
    {
        this.details = details;

        LocalizedMessage = message;
    }

    public XmppException(LocalizedString? message, Func<THandler, ValueTask> details, Exception? innerException) : base(message?.ToString(), innerException)
    {
        this.details = details;

        LocalizedMessage = message;
    }

    public override string ToString()
    {
        var output = new StringBuilder();
        var encoder = new StringEncoder(output);
        if(encoder is not THandler handler)
        {
            return base.ToString();
        }
        var task = Output(handler);
        if(!task.IsCompletedSuccessfully)
        {
            task.AsTask().GetAwaiter().GetResult();
        }
        encoder.Close();
        return output.ToString();
    }

    public virtual ValueTask Output(THandler handler) => details(handler);

    sealed class StringEncoder : Grammar.Encoder
    {
        public override string DefaultNamespace => String.Empty;

        static readonly XmlWriterSettings settings = new() {
            Async = true,
            CheckCharacters = false,
            CloseOutput = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Indent = false,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            NewLineHandling = NewLineHandling.Entitize,
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true
        };

        protected override XmlWriter Writer { get; }

        protected override CancellationToken CancellationToken => default;

        public StringEncoder(StringBuilder output)
        {
            Writer = XmlWriter.Create(output, settings);
        }

        public void Close()
        {
            Writer.Dispose();
        }

        protected override ValueTask<Grammar.Encoder> ForkInner()
        {
            return new(this);
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}

public class XmppStreamException : XmppException<IStreamErrorHandler>
{
    public XmppStreamException(Func<IStreamErrorHandler, ValueTask> details) : base(details)
    {

    }

    public XmppStreamException(string? message, Func<IStreamErrorHandler, ValueTask> details) : base(message, details)
    {

    }

    public XmppStreamException(LocalizedString? message, Func<IStreamErrorHandler, ValueTask> details) : base(message, details)
    {

    }

    public async override ValueTask Output(IStreamErrorHandler handler)
    {
        await base.Output(handler);
        if(LocalizedMessage is { } message)
        {
            foreach(var text in message)
            {
                await handler.Text(text);
            }
        }
    }

    public static XmppStreamException BadFormat(string? message = null) => Create(message, static h => h.BadFormat());
    public static XmppStreamException BadNamespacePrefix(string? message = null) => Create(message, static h => h.BadNamespacePrefix());
    public static XmppStreamException Conflict(string? message = null) => Create(message, static h => h.Conflict());
    public static XmppStreamException ConnectionTimeout(string? message = null) => Create(message, static h => h.ConnectionTimeout());
    public static XmppStreamException HostGone(string? message = null) => Create(message, static h => h.HostGone());
    public static XmppStreamException HostUnknown(string? message = null) => Create(message, static h => h.HostUnknown());
    public static XmppStreamException ImproperAddressing(string? message = null) => Create(message, static h => h.ImproperAddressing());
    public static XmppStreamException InternalServerError(string? message = null) => Create(message, static h => h.InternalServerError());
    public static XmppStreamException InvalidFrom(string? message = null) => Create(message, static h => h.InvalidFrom());
    public static XmppStreamException InvalidId(string? message = null) => Create(message, static h => h.InvalidId());
    public static XmppStreamException InvalidNamespace(string? message = null) => Create(message, static h => h.InvalidNamespace());
    public static XmppStreamException InvalidXml(string? message = null) => Create(message, static h => h.InvalidXml());
    public static XmppStreamException NotAuthorized(string? message = null) => Create(message, static h => h.NotAuthorized());
    public static XmppStreamException PolicyViolation(string? message = null) => Create(message, static h => h.PolicyViolation());
    public static XmppStreamException RemoteConnectionFailed(string? message = null) => Create(message, static h => h.RemoteConnectionFailed());
    public static XmppStreamException ResourceConstraint(string? message = null) => Create(message, static h => h.ResourceConstraint());
    public static XmppStreamException RestrictedXml(string? message = null) => Create(message, static h => h.RestrictedXml());
    public static XmppStreamException SeeOtherHost(string? host, string? message = null) => Create(message, h => h.SeeOtherHost(host));
    public static XmppStreamException SystemShutdown(string? message = null) => Create(message, static h => h.SystemShutdown());
    public static XmppStreamException UndefinedCondition(string? message = null) => Create(message, static h => h.UndefinedCondition());
    public static XmppStreamException UnsupportedEncoding(string? message = null) => Create(message, static h => h.UnsupportedEncoding());
    public static XmppStreamException UnsupportedStanzaType(string? message = null) => Create(message, static h => h.UnsupportedStanzaType());
    public static XmppStreamException UnsupportedVersion(string? message = null) => Create(message, static h => h.UnsupportedVersion());
    public static XmppStreamException XmlNotWellFormed(string? message = null) => Create(message, static h => h.XmlNotWellFormed());

    private static XmppStreamException Create(string? message, Func<IStreamErrorHandler, ValueTask> details)
    {
        return message != null ? new(message, details) : new(details);
    }
}

public class XmppStanzaException : XmppException<IStanzaErrorHandler>
{
    public ErrorType? Type { get; }
    public int? Code { get; }

    public XmppStanzaException(ErrorType? type, int? code, Func<IStanzaErrorHandler, ValueTask> details) : base(details)
    {
        Type = type;
        Code = code;
    }

    public XmppStanzaException(ErrorType? type, int? code, string? message, Func<IStanzaErrorHandler, ValueTask> details) : base(message, details)
    {
        Type = type;
        Code = code;
    }

    public XmppStanzaException(ErrorType? type, int? code, LocalizedString? message, Func<IStanzaErrorHandler, ValueTask> details) : base(message, details)
    {
        Type = type;
        Code = code;
    }

    public async override ValueTask Output(IStanzaErrorHandler handler)
    {
        await base.Output(handler);
        if(LocalizedMessage is { } message)
        {
            foreach(var text in message)
            {
                await handler.Text(text);
            }
        }
    }

    public static XmppStanzaException BadRequest(string? message = null) => Create(Modify, 400, message, static h => h.BadRequest());
    public static XmppStanzaException Conflict(string? message = null) => Create(Cancel, 409, message, static h => h.Conflict());
    public static XmppStanzaException FeatureNotImplemented(ErrorType type, string? message = null) => Create(type, 501, message, static h => h.FeatureNotImplemented());
    public static XmppStanzaException Forbidden(string? message = null) => Create(Auth, 403, message, static h => h.Forbidden());
    public static XmppStanzaException Gone(string? newAddress, string? message = null) => Create(Modify, 302, message, h => h.Gone(newAddress));
    public static XmppStanzaException InternalServerError(string? message = null) => Create(Wait, 500, message, static h => h.InternalServerError());
    public static XmppStanzaException ItemNotFound(string? message = null) => Create(Cancel, 404, message, static h => h.ItemNotFound());
    public static XmppStanzaException JidMalformed(string? message = null) => Create(Modify, 400, message, static h => h.JidMalformed());
    public static XmppStanzaException NotAcceptable(string? message = null) => Create(Modify, 406, message, static h => h.NotAcceptable());
    public static XmppStanzaException NotAllowed(string? message = null) => Create(Cancel, 405, message, static h => h.NotAllowed());
    public static XmppStanzaException NotAuthorized(string? message = null) => Create(Auth, 401, message, static h => h.NotAuthorized());
    public static XmppStanzaException PaymentRequired(string? message = null) => Create(Auth, 402, message, static h => h.PaymentRequired());
    public static XmppStanzaException PolicyViolation(ErrorType type, string? message = null) => Create(type, 500, message, static h => h.PolicyViolation());
    public static XmppStanzaException RecipientUnavailable(string? message = null) => Create(Wait, 404, message, static h => h.RecipientUnavailable());
    public static XmppStanzaException Redirect(string? alternateAddress, string? message = null) => Create(Modify, 302, message, h => h.Redirect(alternateAddress));
    public static XmppStanzaException RegistrationRequired(string? message = null) => Create(Auth, 407, message, static h => h.RegistrationRequired());
    public static XmppStanzaException RemoteServerNotFound(string? message = null) => Create(Cancel, 404, message, static h => h.RemoteServerNotFound());
    public static XmppStanzaException RemoteServerTimeout(string? message = null) => Create(Wait, 504, message, static h => h.RemoteServerTimeout());
    public static XmppStanzaException ResourceConstraint(string? message = null) => Create(Wait, 500, message, static h => h.ResourceConstraint());
    public static XmppStanzaException ServiceUnavailable(string? message = null) => Create(Cancel, 503, message, static h => h.ServiceUnavailable());
    public static XmppStanzaException SubscriptionRequired(string? message = null) => Create(Auth, 407, message, static h => h.SubscriptionRequired());
    public static XmppStanzaException UndefinedCondition(ErrorType type, string? message = null) => Create(type, 500, message, static h => h.UndefinedCondition());
    public static XmppStanzaException UnexpectedRequest(ErrorType type, string? message = null) => Create(type, 400, message, static h => h.UnexpectedRequest());

    private static XmppStanzaException Create(ErrorType type, int code, string? message, Func<IStanzaErrorHandler, ValueTask> details)
    {
        return message != null ? new(type, code, message, details) : new(type, code, details);
    }
}

public class XmppSaslException : XmppException<ISaslFailureHandler>
{
    public XmppSaslException(Func<ISaslFailureHandler, ValueTask> details) : base(details)
    {

    }

    public XmppSaslException(string? message, Func<ISaslFailureHandler, ValueTask> details) : base(message, details)
    {

    }

    public static XmppSaslException Aborted(string? message = null) => Create(message, static h => h.Aborted());
    public static XmppSaslException AccountDisabled(string? message = null) => Create(message, static h => h.AccountDisabled());
    public static XmppSaslException CredentialsExpired(string? message = null) => Create(message, static h => h.CredentialsExpired());
    public static XmppSaslException EncryptionRequired(string? message = null) => Create(message, static h => h.EncryptionRequired());
    public static XmppSaslException IncorrectEncoding(string? message = null) => Create(message, static h => h.IncorrectEncoding());
    public static XmppSaslException InvalidAuthzid(string? message = null) => Create(message, static h => h.InvalidAuthzid());
    public static XmppSaslException InvalidMechanism(string? message = null) => Create(message, static h => h.InvalidMechanism());
    public static XmppSaslException MalformedRequest(string? message = null) => Create(message, static h => h.MalformedRequest());
    public static XmppSaslException MechanismTooWeak(string? message = null) => Create(message, static h => h.MechanismTooWeak());
    public static XmppSaslException NotAuthorized(string? message = null) => Create(message, static h => h.NotAuthorized());
    public static XmppSaslException TemporaryAuthFailure(string? message = null) => Create(message, static h => h.TemporaryAuthFailure());

    private static XmppSaslException Create(string? message, Func<ISaslFailureHandler, ValueTask> details)
    {
        return message != null ? new(message, details) : new(details);
    }
}
