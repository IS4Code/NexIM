using Unicord.Primitives.Xml;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Handlers;

internal static class StanzaExtensions
{
    public static (StanzaType? type, XmppResource? from, XmppResource? to) OpenStanza(this IStanzaCommandHandler handler, in Stanza stanza)
    {
        var (type, from, to, _) = stanza;
        return (type?.ToEnum(), from, to);
    }

    public static void EnsureReceiverIsUserAccount(this IStanzaCommandHandler handler)
    {
        var session = handler.GetSession();
        if(handler.To is { } to && to != session.RemoteResource?.Bare)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    public static void EnsureReceiverIsServer(this IStanzaCommandHandler handler)
    {
        var session = handler.GetSession();
        if(handler.To is { } to && to != session.RemoteResource?.Bare && to != session.LocalResource)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account or server.");
        }
    }

    public static XmppResource GetSender(this IStanzaCommandHandler handler)
    {
        return handler.From ?? handler.GetRemoteResource();
    }

    public static XmppResource? GetRecipient(this IStanzaCommandHandler handler)
    {
        return handler.To;
    }

    public static Token<StanzaIdentifier>? GetIdentifier(this IStanzaCommandHandler handler)
    {
        return handler.GetContext().Identifier;
    }

    public static EventOrigin GetOrigin(this IStanzaCommandHandler handler)
    {
        return new()
        {
            From = handler.GetSender().ToIdentifier(),
            To = new(handler.GetRecipient()?.ToIdentifier()),
            TransactionIdentifier = handler.GetIdentifier()?.ToIdentifier()
        };
    }

    public static string? GetLanguage(this IStanzaCommandHandler handler)
    {
        return handler.GetSession().RemoteLanguage;
    }
}
