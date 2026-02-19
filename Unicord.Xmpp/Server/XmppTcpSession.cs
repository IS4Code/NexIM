using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> 
/// sending XMPP commands via a <see cref="TcpClient"/> instance.
/// </summary>
public abstract class XmppTcpSession : XmppStreamSession
{
    readonly TcpClient client;

    public sealed override bool Connected => client.Connected;

    // Loopback connection is considered secure
    public sealed override bool IsSecure =>
        Stream is SslStream ||
        client.Client.RemoteEndPoint is IPEndPoint { Address: var addr } && IPAddress.IsLoopback(addr);

    public sealed override bool CanUpgradeTls => Stream is not SslStream;

    public sealed override EndPoint? RemoteEndPoint => client.Client.RemoteEndPoint;
    public sealed override CancellationToken CancellationToken { get; }

    protected abstract SslServerAuthenticationOptions ServerAuthenticationOptions { get; }

    public XmppTcpSession(TcpClient client, CancellationToken cancellationToken) : base(client.GetStream())
    {
        this.client = client;

        CancellationToken = cancellationToken;
    }

    protected async sealed override ValueTask UpgradeTls()
    {
        // Flush all remaining commands (STARTTLS)
        await Flush();

        var sslStream = new SslStream(Stream);
        await sslStream.AuthenticateAsServerAsync(ServerAuthenticationOptions, CancellationToken);

        Initialize(sslStream);
    }

    protected async override ValueTask Close()
    {
        await base.Close();
        client.Dispose();
    }
}
