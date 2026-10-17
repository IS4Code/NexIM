using System.Text;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;

namespace NexIM.Irc.Server.Commands;

partial class Stream
{
    bstring nickname, user, realname;

    private ValueTask PassCommand(in CommandMessage message)
    {
        var session = this.GetSession();
        if(session.IsAuthenticated)
        {
            return SendReply(MessageReply.AlreadyRegistered, Texts.AlreadyRegistered);
        }
        if(message.Parameters.Count < 1)
        {
            return SendReply(MessageReply.NeedMoreParams, message.Command, Texts.NotEnoughParameters);
        }
        var password = message.Parameters[0];
        // TODO Configurable connection password
        if(!password.IsEmpty)
        {
            return SendReply(MessageReply.PasswdMismatch, Texts.PasswordIncorrect);
        }
        return default;
    }

    private ValueTask NickCommand(in CommandMessage message)
    {
        var session = this.GetSession();
        if(message.Parameters.Count < 1)
        {
            return SendReply(MessageReply.NoNicknameGiven, Texts.NoNicknameGiven);
        }
        var newNickname = message.Parameters[0];
        if(newNickname.IsEmpty)
        {
            // A parameter with nothing in it is no nickname at all
            return SendReply(MessageReply.NoNicknameGiven, Texts.NoNicknameGiven);
        }
        if(!IrcAddress.TryFromNickname(newNickname, out _))
        {
            return SendReply(MessageReply.ErroneusNickname, newNickname, Texts.ErroneusNickname);
        }

        nickname = newNickname.Preserve();
        if(session.IsAuthenticated)
        {
            // Use the old source for confirmation
            var oldAddress = session.RemoteAddress;
            session.RemoteAddress = BuildRemoteAddress();
            // TODO Rename ClientSession
            return SendCommand(oldAddress, message.Command, newNickname);
        }

        return TryCompleteRegistration();
    }

    private ValueTask UserCommand(in CommandMessage message)
    {
        if(this.TryGetClientSession() != null)
        {
            return SendReply(MessageReply.AlreadyRegistered, Texts.AlreadyRegistered);
        }
        if(message.Parameters.Count < 4)
        {
            return SendReply(MessageReply.NeedMoreParams, message.Command, Texts.NotEnoughParameters);
        }
        user = message.Parameters[0].Preserve();

        // TODO Invisible
        var mode = message.Parameters[1].AsSpan();

        var realname = message.Parameters[3];
        if(!realname.IsEmpty && !Ascii.EqualsIgnoreCase(realname.AsSpan(), "realname"u8))
        {
            // Worth preserving
            this.realname = realname.Preserve();
        }
        return TryCompleteRegistration();
    }

    static readonly CommandMessage AutoMotd = new("MOTD"u8.ToArray());
    static readonly CommandMessage AutoLUsers = new("LUSERS"u8.ToArray());

    static readonly bstring defaultUserModes = "iow"u8.ToArray();
    static readonly bstring defaultChannelModes = "nt"u8.ToArray();
    private async ValueTask TryCompleteRegistration()
    {
        if(capNegotiating || nickname.IsEmpty || user.IsEmpty)
        {
            // Not all information given yet
            return;
        }

        var session = this.GetSession();
        var server = this.GetServer();

        var address = BuildRemoteAddress();
        if(!session.IsAuthenticated)
        {
            // Anonymous authentication
            var assertedName = address.ToIdentifier().Account;

            if(!await Login(await server.AuthenticateAnonymous(FormAccountName, assertedName, session)))
            {
                // Only if rejected
                await SendReply(MessageReply.NicknameInUse, nickname, Texts.NicknameInUse);
                return;
            }

            // Authenticated
            address = BuildRemoteAddress();
        }

        // Registration is finished
        session.RemoteAddress = address;

        await SendReply(MessageReply.Welcome, $"Welcome to the Internet Relay Network {session.RemoteAddress.Value}");
        await SendReply(MessageReply.YourHost, $"Your host is {session.LocalAddress.Host}, running version {server.ProductName}-{server.ProductVersion}");
        await SendReply(MessageReply.Created, $"This server was created {server.StartDate:R}");
        await SendReply(MessageReply.MyInfo,
            session.LocalAddress.Host,
            encoder.Get(server.ProductVersion),
            defaultUserModes,
            defaultChannelModes
        );
        await SendSupportedFeatures();
        await LUsersCommand(AutoLUsers);
        await MotdCommand(AutoMotd);
    }
}
