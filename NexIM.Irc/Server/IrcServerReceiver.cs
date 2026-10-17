using System;
using System.Threading.Tasks;
using NexIM.Irc.Server.Commands;
using NexIM.Irc.Server.Communication;
using NexIM.Server;

namespace NexIM.Irc.Server;

public class IrcServerReceiver
{
    NexServer? _server;
    public NexServer Server {
        get => _server ?? throw new InvalidOperationException("No server has been configured.");
        set => _server = value;
    }

    public ValueTask<IIrcReceivingHandler> Connected(IrcHandlerSession session)
    {
        return new(new Stream { Context = session });
    }
}
