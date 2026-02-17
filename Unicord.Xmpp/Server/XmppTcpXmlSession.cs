using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

internal class XmppTcpXmlSession : XmppXmlSession
{
    readonly TcpClient client;
    Stream transportStream;

    public override bool Connected => client.Connected;

    // Loopback connection is considered secure
    public override bool IsSecure =>
        transportStream is SslStream ||
        client.Client.RemoteEndPoint is IPEndPoint { Address: var addr } && IPAddress.IsLoopback(addr);

    public override bool CanUpgradeTls => transportStream is not SslStream;

    public override EndPoint? RemoteEndPoint => client.Client.RemoteEndPoint;
    public override CancellationToken CancellationToken { get; }

    public XmppTcpXmlSession(TcpClient client, NetworkStream transportStream, XmlWriter writer, CancellationToken cancellationToken) : base(writer)
    {
        this.client = client;
        this.transportStream = transportStream;

        CancellationToken = cancellationToken;
    }

    protected async override ValueTask UpgradeTls()
    {
        // Flush all remaining commands (STARTTLS)
        await Writer.FlushAsync();

        var sslStream = new SslStream(transportStream);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions()
        {
            EnabledSslProtocols = (SslProtocols)(-1),
            RemoteCertificateValidationCallback = delegate
            {
                return true;
            },
            ClientCertificateRequired = true,
            ServerCertificate = GetCertificate()
        }, CancellationToken);
        transportStream = sslStream;
        OnResetStream?.Invoke(transportStream);
    }

    public async override ValueTask DisposeAsync()
    {
        client.Dispose();
    }

    private X509Certificate GetCertificate()
    {
        using var rsa = RSA.Create();
        var req = new CertificateRequest("CN=" + LocalResource?.ToString(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(7));

        // Load as persisted
        cert = new(cert.Export(X509ContentType.Pkcs12, ""), "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

        return cert;
    }
}
