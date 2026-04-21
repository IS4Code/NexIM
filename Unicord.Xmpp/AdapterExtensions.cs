using System;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp;

using Identifiers = Unicord.Server.Tools.NonEmptySet<Identifier>;

#pragma warning disable 8509, 8524

internal static partial class AdapterExtensions
{
    public static Identifier ToIdentifier(this XmppResource resource)
    {
        return new(ToAccountName(resource, out var id), id);
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
        return ToResource(account, resource);
    }

    public static XmppResource? ToResource(this Identifiers identifiers)
    {
        return
            identifiers.TryGetSingle(out var identifier)
            ? ToResource(identifier)
            : null;
    }

    public static XmppAddress ToAddress(this AccountName accountName)
    {
        return new(accountName.User, accountName.Host);
    }

    public static XmppResource ToResource(this AccountName accountName, string? resourceIdentifier)
    {
        return new(ToAddress(accountName), resourceIdentifier);
    }

    public static AccountName ToAccountName(this XmppAddress address)
    {
        return new(address.User, address.Host);
    }

    public static AccountName ToAccountName(this XmppResource resource, out string? resourceIdentifier)
    {
        resourceIdentifier = resource.ResourceIdentifier;
        return ToAccountName(resource.Address);
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
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session),
            Language: evnt.TransactionLanguage
        );
    }

    public static Stanza ToStanza(this PresenceEvent evnt, IXmppSession session)
    {
        var to = evnt.To.ToResource();
        if(to == session.RemoteResource?.Bare)
        {
            // Ignore if mirrored from the account
            to = null;
        }
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
            To: to,
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session),
            Language: evnt.TransactionLanguage
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
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session),
            Language: evnt.TransactionLanguage
        );
    }

    public static Stanza ToStanza(this ErrorEvent evnt, IXmppSession session)
    {
        return new(
            Type: StanzaType.Error.ToToken(),
            From: evnt.From.ToResource(),
            To: evnt.To.ToResource(),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session),
            Language: evnt.TransactionLanguage
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

    public static EventExtensions ToExtensions<THandler>(this CapturingHandler<THandler>? handler) where THandler : IPayloadHandler
    {
        return new(handler?.Calls.Count > 0 ? handler : null);
    }
}
