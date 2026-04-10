using Unicord.Primitives;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Handlers;

internal static class StanzaExtensions
{
    public static void EnsureReceiverIsUserAccount(this ICommandHandler handler)
    {
        var session = handler.GetSession();
        if(handler.GetStanza().To is { } to && to != session.RemoteResource?.Bare)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    public static void EnsureReceiverIsServer(this ICommandHandler handler)
    {
        var session = handler.GetSession();
        if(handler.GetStanza().To is { } to && to != session.RemoteResource?.Bare && to != session.LocalResource)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account or server.");
        }
    }

    public static XmppResource GetSender(this ICommandHandler handler)
    {
        return handler.GetStanza().From ?? handler.GetRemoteResource();
    }

    public static XmppResource? GetRecipient(this ICommandHandler handler)
    {
        return handler.GetStanza().To;
    }

    public static Token<StanzaIdentifier>? GetIdentifier(this ICommandHandler handler)
    {
        return handler.GetStanza().Identifier;
    }

    public static EventOrigin GetOrigin(this ICommandHandler handler)
    {
        return new()
        {
            From = handler.GetSender().ToIdentifier(),
            To = new(handler.GetRecipient()?.ToIdentifier()),
            TransactionIdentifier = handler.GetIdentifier()?.ToIdentifier(),
            TransactionLanguage = handler.GetStanza().Language
        };
    }
}
