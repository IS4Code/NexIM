using System;
using NexIM.Irc.Protocol;
using NexIM.Irc.Server.Communication;
using NexIM.Server;
using NexIM.Server.Accounts;

namespace NexIM.Irc.Server.Commands;

internal static class CommandExtensions
{
    public static ICommandContext GetContext(this ICommandHandler handler)
    {
        return handler.Context ?? throw new InvalidOperationException("The command context is not properly initialized.");
    }

    public static Account GetAccount(this ICommandHandler handler)
    {
        return GetClientSession(handler).Account;
    }

    public static IrcClientSession? TryGetClientSession(this ICommandHandler handler)
    {
        return GetContext(handler).Session.ClientSession;
    }

    public static IrcClientSession GetClientSession(this ICommandHandler handler)
    {
        return TryGetClientSession(handler) ?? throw IrcException.NotRegistered();
    }

    public static IIrcSession GetSession(this ICommandHandler handler)
    {
        return GetContext(handler).Session;
    }

    public static IrcServerReceiver GetServerReceiver(this ICommandHandler handler)
    {
        return GetContext(handler).ServerReceiver;
    }

    public static NexServer GetServer(this ICommandHandler handler)
    {
        return GetContext(handler).ServerReceiver.Server;
    }

    public static bool IsSecureSession(this ICommandHandler handler)
    {
        return GetSession(handler).IsSecure;
    }
}
