using System;
using System.Buffers;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Irc.Tools;
using NexIM.Server;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Tools;

namespace NexIM.Irc.Server.Commands;

internal sealed partial class Stream : ICommandHandler, IIrcReceivingHandler
{
    public required ICommandContext Context { get; init; }

    // TODO Replace with fallback if wrong
    public Encoding Encoding { get; } = Encoding.UTF8;
    StringTransformationCache<ByteStringView> encoder => ByteStringCache.Global.Get(Encoding);

    readonly ArrayPool<bstring> parameterPool = ArrayPool<bstring>.Create();

    async ValueTask IIrcReceivingHandler.StreamStarted()
    {
        // TODO Use server name
        var session = this.GetSession();
        session.LocalAddress = new IrcAddress(host: GetHostName(session.LocalEndPoint));
    }

    async ValueTask IIrcReceivingHandler.StreamStopped()
    {
        if(this.TryGetClientSession() is { } session)
        {
            await session.Account.RemoveSession(session);
        }
    }

    private bstring GetHostName(EndPoint? endPoint)
    {
        return encoder.Get(endPoint.GetHostName() ?? "*");
    }

    private AccountName FormAccountName(string username)
    {
        // TODO Use server name
        return new AccountName(username, this.GetSession().LocalEndPoint.GetHostName() ?? "");
    }

    private async ValueTask<bool> Login(Account? account)
    {
        if(account == null)
        {
            return false;
        }

        var session = this.GetSession();
        var clientSession = new IrcClientSession(account, nickname.ToString(Encoding));

        if(await account.AddSession(clientSession) != StatusCode.Success)
        {
            return false;
        }

        session.ClientSession = clientSession;
        return true;
    }

    private IrcAddress BuildRemoteAddress()
    {
        var session = this.GetSession();
        if(session.IsAuthenticated)
        {
            return session.ClientSession.Identifier.ToAddress(session);
        }

        var host = GetHostName(session.RemoteEndPoint);
        var user = nickname.IsEmpty ? default : this.user;

        if(!IrcAddress.TryCreate(nickname, user, host, out var address))
        {
            // A username that cannot be part of an address is left out of it
            IrcAddress.TryCreate(nickname, default, host, out address);
        }
        return address;
    }

    private ValueTask SendCommandWithParams(IrcAddress source, bstring command, ReadOnlySpan<bstring> @params)
    {
        if(@params.IsEmpty)
        {
            return this.GetSession().Command(new(command, Source: source));
        }
        var buffer = parameterPool.Rent(@params.Length);
        @params.CopyTo(buffer);
        return Send(@params.Length);
        async ValueTask Send(int count)
        {
            try
            {
                await this.GetSession().Command(new(command, new(buffer, count), source));
            }
            finally
            {
                parameterPool.Return(buffer, clearArray: true);
            }
        }
    }

    private ValueTask SendCommandWithParams(IrcAddress source, MessageReply reply, ReadOnlySpan<bstring> @params)
    {
        if(@params.IsEmpty)
        {
            return this.GetSession().Reply(new(reply, Source: source));
        }
        var buffer = parameterPool.Rent(@params.Length);
        @params.CopyTo(buffer);
        return Send(@params.Length);
        async ValueTask Send(int count)
        {
            try
            {
                await this.GetSession().Reply(new(reply, new(buffer, count), source));
            }
            finally
            {
                parameterPool.Return(buffer, clearArray: true);
            }
        }
    }

    ValueTask IStreamHandler.Command(in CommandMessage message)
    {
        var session = this.GetSession();

        if(!message.Source.IsEmpty && !message.Source.Equals(session.RemoteAddress))
        {
            // Unrecognized source
            return default;
        }

        var command = message.Command.AsSpan();
        var rest = command.Slice(1);
        switch(command[0])
        {
            case (byte)'a' or (byte)'A':
                if(Ascii.EqualsIgnoreCase(rest, "UTHENTICATE"u8))
                {
                    // AUTHENTICATE
                    return AuthenticateCommand(message);
                }
                break;

            case (byte)'c' or (byte)'C':
                if(Ascii.EqualsIgnoreCase(rest, "AP"u8))
                {
                    // CAP
                    return CapCommand(message);
                }
                break;

            case (byte)'j' or (byte)'J':
                if(Ascii.EqualsIgnoreCase(rest, "OIN"u8))
                {
                    // JOIN
                }
                break;

            case (byte)'l' or (byte)'L':
                if(Ascii.EqualsIgnoreCase(rest, "USERS"u8))
                {
                    // LUSERS
                    return LUsersCommand(message);
                }
                break;

            case (byte)'m' or (byte)'M':
                if(Ascii.EqualsIgnoreCase(rest, "ODE"u8))
                {
                    // MODE
                }
                if(Ascii.EqualsIgnoreCase(rest, "OTD"u8))
                {
                    // MOTD
                    return MotdCommand(message);
                }
                break;

            case (byte)'n' or (byte)'N':
                if(Ascii.EqualsIgnoreCase(rest, "ICK"u8))
                {
                    // NICK
                    return NickCommand(message);
                }
                if(Ascii.EqualsIgnoreCase(rest, "OTICE"u8))
                {
                    // NOTICE
                }
                break;

            case (byte)'p' or (byte)'P':
                if(Ascii.EqualsIgnoreCase(rest, "ART"u8))
                {
                    // PART
                }
                if(Ascii.EqualsIgnoreCase(rest, "ASS"u8))
                {
                    // PASS
                    return PassCommand(message);
                }
                if(Ascii.EqualsIgnoreCase(rest, "ING"u8))
                {
                    // PING
                    return PingCommand(message);
                }
                if(Ascii.EqualsIgnoreCase(rest, "ONG"u8))
                {
                    // PONG
                }
                if(Ascii.EqualsIgnoreCase(rest, "RIVMSG"u8))
                {
                    // PRIVMSG
                }
                break;

            case (byte)'q' or (byte)'Q':
                if(Ascii.EqualsIgnoreCase(rest, "UIT"u8))
                {
                    // QUIT
                }
                break;

            case (byte)'t' or (byte)'T':
                if(Ascii.EqualsIgnoreCase(rest, "OPIC"u8))
                {
                    // TOPIC
                }
                break;

            case (byte)'u' or (byte)'U':
                if(Ascii.EqualsIgnoreCase(rest, "SER"u8))
                {
                    // USER
                    return UserCommand(message);
                }
                break;

            case (byte)'v' or (byte)'V':
                if(Ascii.EqualsIgnoreCase(rest, "ERSION"u8))
                {
                    // VERSION
                    return VersionCommand(message);
                }
                break;
        }
        return default;
    }

    ValueTask IStreamHandler.Reply(in ReplyMessage message)
    {
        var session = this.GetSession();

        if(!message.Source.IsEmpty && !message.Source.Equals(session.RemoteAddress))
        {
            // Unrecognized source
            return default;
        }

        // TODO Route to target
        return default;
    }
}
