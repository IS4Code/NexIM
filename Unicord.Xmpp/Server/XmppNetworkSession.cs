using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> 
/// sending XMPP commands via a <see cref="NetworkStream"/> instance.
/// </summary>
public abstract class XmppNetworkSession(NetworkStream networkStream, CancellationToken cancellationToken) : XmppStreamSession(networkStream)
{
    SslStream? sslStream;

    public sealed override bool Connected => networkStream.Socket.Connected;

    public sealed override bool IsSecure =>
        sslStream != null ||
        // Loopback connection is considered secure
        RemoteEndPoint is IPEndPoint { Address: var addr } && IPAddress.IsLoopback(addr);

    public sealed override bool CanUpgradeTls => sslStream == null;

    public X509Certificate? RemoteCertificate => sslStream?.RemoteCertificate;
    public sealed override EndPoint? RemoteEndPoint => networkStream.Socket.RemoteEndPoint;
    public sealed override CancellationToken CancellationToken => cancellationToken;

    protected abstract SslServerAuthenticationOptions ServerAuthenticationOptions { get; }

    protected async sealed override ValueTask UpgradeTls()
    {
        // Flush all remaining commands (STARTTLS)
        await Flush();

        var stream = new SslStream(Stream, leaveInnerStreamOpen: false);
        await stream.AuthenticateAsServerAsync(ServerAuthenticationOptions, CancellationToken);

        Initialize(stream);
        sslStream = stream;
    }

    protected async override ValueTask Close()
    {
        await base.Close();
        await networkStream.DisposeAsync();
    }
}
