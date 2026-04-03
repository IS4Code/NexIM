using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp;

internal static class AdapterExtensions
{
    public static Identifier ToIdentifier(this XmppResource resource)
    {
        return new(XmppClientSession.GetAccount(resource, out var id), id);
    }

    public static Identifier ToIdentifier(this Token<StanzaIdentifier> identifier)
    {
        return new(null, identifier.Value);
    }

    public static XmppResource ToResource(this Identifier identifier)
    {
        if(identifier is not (Account: { } account, Resource: var resource))
        {
            // TODO Adapt other identifier formats
            throw new NotImplementedException();
        }
        return XmppClientSession.GetResource(account, resource);
    }

    public static XmppResource? ToResource(this IdentifierSet identifiers)
    {
        return
            identifiers.TryGetSingle(out var identifier)
            ? ToResource(identifier)
            : null;
    }

    public static Token<StanzaIdentifier> ToStanzaIdentifier(this Identifier identifier, IXmppSession session)
    {
        if(identifier is not (Account: null, Resource: { } resource))
        {
            // TODO Adapt other identifier formats
            throw new NotImplementedException();
        }
        return session.GetToken<StanzaIdentifier>(resource);
    }

    public static Stanza ToStanza(this MessageEvent evnt, IXmppSession session)
    {
        return new(
            Type: evnt.Type.ToStanzaType(),
            From: evnt.From.ToResource(),
            To: evnt.To.ToResource(),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session)
        );
    }

    public static Stanza ToStanza(this PresenceEvent evnt, IXmppSession session)
    {
        return new(
            Type: evnt switch {
                StatusUpdateEvent { Data.Status.Availability: Availability.Unavailable } => StanzaType.Unavailable.ToToken(),
                StatusUpdateEvent => null,
                SubscriptionRequestedEvent => StanzaType.Subscribe.ToToken(),
                SubscriptionAcceptedEvent => StanzaType.Subscribed.ToToken(),
                SubscriptionRejectedEvent => StanzaType.Unsubscribed.ToToken(),
                SubscriptionCancelledEvent => StanzaType.Unsubscribe.ToToken()
            },
            From: evnt.From.ToResource(),
            To: evnt.To.ToResource(),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session)
        );
    }

    public static Stanza ToStanza(this QueryEvent evnt, IXmppSession session)
    {
        return new(
            Type: evnt switch {
                RetrieveEvent => StanzaType.Get.ToToken(),
                UpdateEvent => StanzaType.Set.ToToken(),
                ResponseEvent => StanzaType.Result.ToToken()
            },
            From: evnt.From.ToResource(),
            To: evnt.To.ToResource(),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session)
        );
    }

    public static Stanza ToStanza(this ErrorEvent evnt, IXmppSession session)
    {
        return new(
            Type: StanzaType.Error.ToToken(),
            From: evnt.From.ToResource(),
            To: evnt.To.ToResource(),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session)
        );
    }

    public static Token<StanzaType>? ToStanzaType(this MessageType type)
    {
        return type switch
        {
            MessageType.Normal => StanzaType.Normal.ToToken(),
            MessageType.Chat => StanzaType.Chat.ToToken(),
            MessageType.GroupChat => StanzaType.GroupChat.ToToken(),
            MessageType.Headline => StanzaType.Headline.ToToken(),
            _ => null
        };
    }

    public static MessageType ToMessageType(this StanzaType? type)
    {
        return type switch {
            StanzaType.Normal or null => MessageType.Normal,
            StanzaType.Chat => MessageType.Chat,
            StanzaType.GroupChat => MessageType.GroupChat,
            StanzaType.Headline => MessageType.Headline,
            _ => throw XmppStanzaException.BadRequest("Invalid message type.")
        };
    }

    public static Availability? ToAvailability(this StatusType type)
    {
        return type switch {
            StatusType.Chat => Availability.Chatting,
            StatusType.Away => Availability.Away,
            StatusType.ExtendedAway => Availability.Gone,
            StatusType.DoNotDisturb => Availability.Busy,
            _ => null
        };
    }

    public static StatusType? ToStatusType(this Availability availability)
    {
        return availability switch {
            Availability.Chatting => StatusType.Chat,
            Availability.Away => StatusType.Away,
            Availability.Gone => StatusType.ExtendedAway,
            Availability.Busy => StatusType.DoNotDisturb,
            _ => null
        };
    }

    public static XmppStanzaException ToStanzaException(this ErrorCode code)
    {
        return code switch {
            ErrorCode.NotFound => XmppStanzaException.ItemNotFound(),
            ErrorCode.InvalidRequest => XmppStanzaException.BadRequest(),
            ErrorCode.NotAvailable => XmppStanzaException.ServiceUnavailable(),
            ErrorCode.NotAuthorized => XmppStanzaException.NotAuthorized(),
            ErrorCode.Unrecognized => XmppStanzaException.FeatureNotImplemented()
        };
    }

    public static ErrorCode ToErrorCode(this XmppStanzaException exception)
    {
        var handler = new ErrorHandler();
        var task = exception.Output(handler);
        if(!task.IsCompletedSuccessfully)
        {
            // Should not happen since handler calls are synchronous
            task.AsTask().GetAwaiter().GetResult();
        }
        return handler.Code ?? ErrorCode.Unrecognized;
    }

    public static RecommendedErrorAction ToRecommendedAction(this ErrorType errorType)
    {
        return errorType switch {
            ErrorType.Auth => RecommendedErrorAction.Authenticate,
            ErrorType.Cancel => RecommendedErrorAction.Abandon,
            ErrorType.Continue => RecommendedErrorAction.Proceed,
            ErrorType.Modify => RecommendedErrorAction.Modify,
            ErrorType.Wait => RecommendedErrorAction.TryAgain
        };
    }

    public static ErrorType ToErrorType(this RecommendedErrorAction action)
    {
        return action switch {
            RecommendedErrorAction.Abandon => ErrorType.Cancel,
            RecommendedErrorAction.Authenticate => ErrorType.Auth,
            RecommendedErrorAction.Modify => ErrorType.Modify,
            RecommendedErrorAction.Proceed => ErrorType.Continue,
            RecommendedErrorAction.TryAgain => ErrorType.Wait
        };
    }

    public static EventExtensions ToExtensions<THandler>(this CapturingHandler<THandler> handler) where THandler : IPayloadHandler
    {
        return new(handler.Calls.Count > 0 ? handler : null);
    }

    sealed class ErrorHandler : StanzaErrorHandler<EmptyPayloadHandlerContext>
    {
        public ErrorCode? Code { get; private set; }

        protected override ValueTask OnItemNotFound()
        {
            Code = ErrorCode.NotFound;
            return default;
        }

        protected override ValueTask OnBadRequest()
        {
            Code = ErrorCode.InvalidRequest;
            return default;
        }

        protected override ValueTask OnServiceUnavailable()
        {
            Code = ErrorCode.NotAvailable;
            return default;
        }

        protected override ValueTask OnNotAuthorized()
        {
            Code = ErrorCode.NotAuthorized;
            return default;
        }

        protected override ValueTask OnFeatureNotImplemented()
        {
            Code = ErrorCode.Unrecognized;
            return default;
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader)
        {
            return default;
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
