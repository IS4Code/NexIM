using System;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp;

using Identifiers = NexIM.Server.Tools.NonEmptySet<Identifier>;

#pragma warning disable 8509, 8524

internal static partial class AdapterExtensions
{
    public static AccountName ToAccountName(this XmppAddress address, IXmppSession? session = null)
    {
        if(session != null && new XmppResource(address, null) == session.LocalResource)
        {
            // Targets the local party
            return AccountName.Local;
        }
        return new(address.User, address.Host);
    }

    public static AccountName ToAccountName(this XmppResource resource, out string? resourceIdentifier, IXmppSession? session = null)
    {
        resourceIdentifier = resource.ResourceIdentifier;
        return ToAccountName(resource.Address, session);
    }

    public static Identifier ToIdentifier(this XmppResource resource, IXmppSession? session = null)
    {
        return new(ToAccountName(resource, out var id, session), id);
    }

    public static XmppAddress ToAddress(this AccountName accountName, IXmppSession? session = null)
    {
        if(accountName.IsLocal)
        {
            return new(accountName.User, session?.LocalResource?.Address.Host ?? accountName.Host);
        }
        return new(accountName.User, accountName.Host);
    }

    public static XmppResource ToResource(this AccountName accountName, string? resourceIdentifier, IXmppSession? session = null)
    {
        return new(ToAddress(accountName, session), resourceIdentifier);
    }

    public static XmppResource ToResource(this Identifier identifier, IXmppSession? session = null)
    {
        if(identifier is not (Account: { } account, Resource: var resource))
        {
            // TODO Adapt other identifier formats
            throw new NotImplementedException();
        }
        return ToResource(account, resource, session);
    }

    public static XmppResource? ToResource(this Identifiers identifiers, IXmppSession? session = null)
    {
        return
            identifiers.TryGetSingle(out var identifier)
            ? ToResource(identifier, session)
            : null;
    }

    public static Identifier ToIdentifier(this Token<StanzaIdentifier> identifier)
    {
        return new(null, identifier.Value);
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
            From: evnt.From.ToResource(session),
            To: evnt.To.ToResource(session),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session),
            Language: evnt.TransactionLanguage
        );
    }

    public static Stanza ToStanza(this PresenceEvent evnt, IXmppSession session)
    {
        var to = evnt.To.ToResource(session);
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
            From: evnt.From.ToResource(session),
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
            From: evnt.From.ToResource(session),
            To: evnt.To.ToResource(session),
            Identifier: evnt.TransactionIdentifier?.ToStanzaIdentifier(session),
            Language: evnt.TransactionLanguage
        );
    }

    public static Stanza ToStanza(this ErrorEvent evnt, IXmppSession session)
    {
        return new(
            Type: StanzaType.Error.ToToken(),
            From: evnt.From.ToResource(session),
            To: evnt.To.ToResource(session),
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
