using System.Text;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;

namespace NexIM.Irc.Server;

/// <summary>
/// Represents an IRC message stream.
/// </summary>
public interface IIrcHandler : IStreamHandler
{
    Encoding Encoding { get; }
}

/// <summary>
/// Represents an incoming IRC message stream.
/// </summary>
public interface IIrcReceivingHandler : IIrcHandler
{
    ValueTask StreamStarted();
    ValueTask StreamStopped();
}

/// <summary>
/// Represents an outgoing IRC message stream.
/// </summary>
public interface IIrcSendingHandler : IIrcHandler
{
    IrcEndPoint LocalAddress { get; set; }
    IrcEndPoint RemoteAddress { get; set; }
    Capabilities Capabilities { get; set; }
}

/// <summary>
/// Provides a basic implementation of <see cref="IIrcSendingHandler"/>.
/// </summary>
public abstract class IrcSendingHandler : IIrcSendingHandler
{
    public IrcEndPoint LocalAddress { get; set; }
    public IrcEndPoint RemoteAddress { get; set; }
    public Capabilities Capabilities { get; set; }
    public abstract Encoding Encoding { get; }

    protected abstract ValueTask SendCommand(in CommandMessage message);
    protected abstract ValueTask SendReply(in ReplyMessage message);
    ValueTask IStreamHandler.Command(in CommandMessage message) => SendCommand(message);
    ValueTask IStreamHandler.Reply(in ReplyMessage message) => SendReply(message);
}
