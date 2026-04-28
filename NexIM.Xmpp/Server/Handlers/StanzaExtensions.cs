using System;
using NexIM.Primitives;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Tools;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Server.Formats;

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

    public static bool VerifyOwnership(this ICommandHandler handler, XmppResource? resource)
    {
        var session = handler.GetSession();
        // TODO Subdomains and other privileged hosts
        return resource != session.LocalResource;
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

    public static EventOrigin GetOrigin(this ICommandHandler handler, NonEmptySet<Identifier>? recipients = null)
    {
        var session = handler.GetSession();
        var recipient = handler.GetRecipient();
        if(recipients is { } to)
        {
            // Multicast request
            if(recipient != handler.TryGetLocalResource())
            {
                // Only the local server is supported
                throw XmppStanzaException.NotAllowed("Multicast requests can be sent only through the local multicast service.");
            }
        }
        else
        {
            to = (recipient ?? handler.GetRemoteResource().Bare).ToIdentifier(session);
        }
        return new() {
            From = handler.GetSender().ToIdentifier(session),
            To = to,
            TransactionIdentifier = handler.GetIdentifier()?.ToIdentifier(),
            TransactionLanguage = handler.GetStanza().Language
        };
    }

    public static EventProcessing GetProcessing(this ICommandHandler handler, DateTimeOffset? created = null)
    {
        return EventProcessing.Finish(created ?? handler.GetContext().LastStanzaReceived);
    }
}
