using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Provides a final <see cref="IXmppSession"/> implementation
/// that communicates using TCP.
/// </summary>
internal sealed class XmppTcpSession(NetworkStream networkStream, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, CancellationToken cancellationToken) : XmppNetworkSession(networkStream, cancellationToken)
{
    public override string DefaultLanguage => "en";

    protected override SslServerAuthenticationOptions ServerAuthenticationOptions => new SslServerAuthenticationOptions()
    {
        EnabledSslProtocols = (SslProtocols)(-1),
        RemoteCertificateValidationCallback = delegate
        {
            return true;
        },
        ClientCertificateRequired = true,
        ServerCertificate = GetCertificate()
    };

    protected override void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer)
    {
        stream = new ConsoleDebuggingStream(stream);

        reader = XmlReader.Create(stream, readerSettings);
        writer = XmlWriter.Create(stream, writerSettings);
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
