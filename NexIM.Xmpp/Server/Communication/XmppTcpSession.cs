using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using NexIM.Server;
using NexIM.Tools;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Provides a final <see cref="IXmppSession"/> implementation
/// that communicates using TCP.
/// </summary>
internal sealed class XmppTcpSession(XmppServerReceiver serverReceiver, NetworkStream networkStream, X509Certificate2? serverCertificate, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, CancellationToken cancellationToken) : XmppNetworkSession(networkStream, cancellationToken)
{
    public override string DefaultLanguage => "en";
    public override XmppServerReceiver ServerReceiver => serverReceiver;

    protected override SslServerAuthenticationOptions ServerAuthenticationOptions => new() {
        EnabledSslProtocols = (SslProtocols)(-1),
        RemoteCertificateValidationCallback = delegate {
            return true;
        },
        ClientCertificateRequired = true,
        ServerCertificate = serverCertificate
    };

    protected override XmlNameTable NameTable => readerSettings.NameTable ?? base.NameTable;

    protected override void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer)
    {
        stream = new ConsoleDebuggingStream(stream, RemoteEndPoint);

        reader = XmlReader.Create(stream, readerSettings);
        writer = XmlWriter.Create(stream, writerSettings);
    }
}
