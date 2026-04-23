using System;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Xmpp.Tools;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> 
/// sending XMPP commands via a <see cref="NetworkStream"/> instance.
/// </summary>
public abstract class XmppNetworkSession(NetworkStream networkStream, CancellationToken cancellationToken) : XmppAuthSession(networkStream)
{
    SslStream? sslStream;

    bool isCompressed;

    public sealed override bool Connected => networkStream.Socket.Connected;

    public sealed override bool IsSecure =>
        sslStream != null ||
        // Local connection is considered secure
        LocalEndPoint.SameAddressAs(RemoteEndPoint);

    public sealed override bool CanUpgradeTls => sslStream == null;

    public sealed override bool CanCompress =>
        // Do not compress when TLS is a possibility
        !isCompressed && !CanUpgradeTls;

    const string socketException = "The socket is not bound";

    public sealed override EndPoint LocalEndPoint => networkStream.Socket.LocalEndPoint ?? throw new InvalidOperationException(socketException);
    public sealed override EndPoint RemoteEndPoint => networkStream.Socket.RemoteEndPoint ?? throw new InvalidOperationException(socketException);
    public sealed override X509Certificate? RemoteCertificate => sslStream?.RemoteCertificate;
    public sealed override CancellationToken CancellationToken => cancellationToken;

    protected abstract SslServerAuthenticationOptions ServerAuthenticationOptions { get; }

    protected async sealed override ValueTask UpgradeTls()
    {
        var stream = new SslStream(Stream, leaveInnerStreamOpen: false);
        await stream.AuthenticateAsServerAsync(ServerAuthenticationOptions, CancellationToken);

        Initialize(stream);
        sslStream = stream;
    }

    protected async sealed override ValueTask EnableCompression()
    {
        var decompress = new ZLibStream(Stream, CompressionMode.Decompress, leaveOpen: true);
        var compress = new ZLibStream(Stream, CompressionMode.Compress, leaveOpen: false);

        // Send the header
        await compress.FlushAsync(CancellationToken);

        // Tie the streams back together
        var stream = new BidirectionalStream(decompress, compress);

        Initialize(stream);
        isCompressed = true;
    }

    protected async override ValueTask Close()
    {
        await base.Close();
        await networkStream.DisposeAsync();
    }
}
