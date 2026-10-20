using System;
using System.Text;
using NexIM.Irc.Protocol;
using NexIM.Irc.Server.Communication;
using NexIM.Irc.Tools;
using NexIM.Server;
using NexIM.Server.Accounts;

namespace NexIM.Irc;

internal static class AdapterExtensions
{
    public static IrcEndPoint ToAddress(this Identifier identifier, IIrcSession? session = null)
    {
        var encoder = ByteStringCache.Global.Get(session?.Encoding ?? Encoding.UTF8);
        if(identifier.Account is not { } accountName)
        {
            if(identifier.Resource is not { } resource)
            {
                // No components
                return default;
            }
            // Just identifier
            return IrcEndPoint.FromNickname(encoder.Get(resource));
        }
        if(accountName.IsLocal)
        {
            // No explicit host
            if(accountName.User is not { } user)
            {
                if(identifier.Resource is not { } resource)
                {
                    // No components
                    return default;
                }
                // Just resource
                return IrcEndPoint.FromNickname(encoder.Get(resource));
            }
            // User makes host necessary
            if(session != null)
            {
                if(identifier.Resource is not { } resource)
                {
                    // TODO Cannot encode user if there is no nickname
                    throw new NotImplementedException();
                }
                var addr = encoder.Get($"{resource}!{user}@{session.LocalAddress.Host}");
                return new(addr);
            }
            else
            {
                throw new ArgumentNullException(nameof(session));
            }
        }
        else
        {
            // Host given
            var host = accountName.Host;
            if(accountName.User is not { } user)
            {
                if(identifier.Resource is not { } resource)
                {
                    // Just host
                    return IrcEndPoint.FromHost(encoder.Get(host));
                }
                // Resource and host
                var addr = encoder.Get($"{resource}@{host}");
                return new(addr, IrcEndPoint.Components.Nickname | IrcEndPoint.Components.Host);
            }
            else
            {
                if(identifier.Resource is not { } resource)
                {
                    // TODO Cannot encode user if there is no nickname
                    throw new NotImplementedException();
                }
                // All given
                var addr = encoder.Get($"{resource}!{user}@{host}");
                return new(addr);
            }
        }
    }

    public static Identifier ToIdentifier(this IrcEndPoint address, IIrcSession? session = null)
    {
        var encoding = session?.Encoding ?? Encoding.UTF8;

        var nickname = address.Nickname;
        var user = address.User;
        var host = address.Host;

        AccountName? accountName;
        if(user.IsEmpty && host.IsEmpty)
        {
            accountName = null;
        }
        else
        {
            accountName = new(
                user.IsEmpty ? "" : user.ToString(encoding),
                host.ToString(encoding)
            );
        }
        if(nickname.IsEmpty)
        {
            return new(accountName, null);
        }
        return new(accountName, nickname.ToString(encoding));
    }
}
