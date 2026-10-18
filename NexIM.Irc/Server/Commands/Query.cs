using System.Threading.Tasks;
using NexIM.Irc.Protocol;

namespace NexIM.Irc.Server.Commands;

partial class Stream
{
    private ValueTask MotdCommand(in CommandMessage message)
    {
        // TODO MOTD
        if(message.Parameters.Count >= 1)
        {
            return SendReply(MessageReply.NoSuchServer, message.Parameters[0], Texts.NoSuchServer);
        }
        return SendReply(MessageReply.NoMotd, Texts.NoMotd);
    }

    private ValueTask LUsersCommand(in CommandMessage message)
    {
        // TODO LUSERS
        return Inner();
        async ValueTask Inner()
        {
            await SendReply(MessageReply.LUserClient, $"There are {0} users and {0} invisible on {0} servers");
            await SendReply(MessageReply.LUserMe, $"I have {0} clients and {0} servers");
        }
    }

    private ValueTask VersionCommand(in CommandMessage message)
    {
        // TODO VERSION
        if(message.Parameters.Count >= 1)
        {
            return SendReply(MessageReply.NoSuchServer, message.Parameters[0], Texts.NoSuchServer);
        }
        return Inner();
        async ValueTask Inner()
        {
            var server = this.GetServer();
            await SendReply(MessageReply.Version, encoder.Get(server.ProductVersion), encoder.Get(server.ProductName), ByteStringView.Empty);
            await SendSupportedFeatures();
        }
    }

    static readonly bstring caseMappingFeature = "CASEMAPPING=rfc7613"u8.ToArray();
    static readonly bstring hostLenFeature = "HOSTLEN=253"u8.ToArray();
    async ValueTask SendSupportedFeatures()
    {
        await SendReply(MessageReply.ISupport, caseMappingFeature, hostLenFeature, "are supported by this server");
    }

    static readonly bstring pong = "PONG"u8.ToArray();
    private ValueTask PingCommand(in CommandMessage message)
    {
        if(message.Parameters.Count < 1)
        {
            return SendReply(MessageReply.NeedMoreParams, message.Command, Texts.NotEnoughParameters);
        }
        // No event because this has no target
        var token = message.Parameters[0];
        return SendCommand(pong, token);
    }
}
