using System.Net.Sockets;
using System.Threading;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Provides a final <see cref="IIrcSendingHandler"/> implementation
/// that communicates using TCP.
/// </summary>
internal sealed class IrcTcpSession(IrcServerReceiver receiver, TcpClient client, CancellationToken cancellationToken) : IrcNetworkSession(client.GetStream())
{
    public override IrcServerReceiver ServerReceiver => receiver;
    public override CancellationToken CancellationToken => cancellationToken;
}
