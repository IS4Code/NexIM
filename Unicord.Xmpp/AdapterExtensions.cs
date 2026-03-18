using System;
using Unicord.Primitives.Xml;
using Unicord.Server.Model;
using Unicord.Server.Model.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp;

internal static class AdapterExtensions
{
    public static Identifier ToIdentifier(this XmppResource resource)
    {
        return new(ClientSession.GetAccount(resource, out var id), id);
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
        return ClientSession.GetResource(account, resource);
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
            From: evnt.From?.ToResource(),
            To: evnt.To?.ToResource(),
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

    public static XmppStanzaException ToStanzaException(this ErrorCode code)
    {
        return code switch {
            ErrorCode.NotFound => XmppStanzaException.ItemNotFound(),
            ErrorCode.InvalidRequest => XmppStanzaException.BadRequest(),
            ErrorCode.NotAvailable => XmppStanzaException.ServiceUnavailable()
        };
    }
}
