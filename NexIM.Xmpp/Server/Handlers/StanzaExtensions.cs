using NexIM.Primitives;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;

namespace NexIM.Xmpp.Server.Handlers;

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
        var session = handler.GetSession();
        return new() {
            From = handler.GetSender().ToIdentifier(session),
            To = (handler.GetRecipient() ?? handler.GetRemoteResource().Bare).ToIdentifier(session),
            TransactionIdentifier = handler.GetIdentifier()?.ToIdentifier(),
            TransactionLanguage = handler.GetStanza().Language
        };
    }

    public static EventProcessing GetProcessing(this ICommandHandler handler)
    {
        return EventProcessing.Finish(handler.GetContext().LastStanzaReceived);
    }
}
