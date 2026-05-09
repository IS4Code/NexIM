using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
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
internal sealed class XmppTcpSession(XmppServer server, NetworkStream networkStream, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, CancellationToken cancellationToken) : XmppNetworkSession(networkStream, cancellationToken)
{
    public override string DefaultLanguage => "en";
    public override XmppServer Server => server;

    protected override SslServerAuthenticationOptions ServerAuthenticationOptions => new() {
        EnabledSslProtocols = (SslProtocols)(-1),
        RemoteCertificateValidationCallback = delegate {
            return true;
        },
        ClientCertificateRequired = true,
        ServerCertificate = Configuration.GetCertificate(LocalResource?.ToString()!)
    };

    protected override XmlNameTable NameTable => readerSettings.NameTable ?? base.NameTable;

    protected override void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer)
    {
        stream = new ConsoleDebuggingStream(stream, RemoteEndPoint);

        reader = XmlReader.Create(stream, readerSettings);
        writer = XmlWriter.Create(stream, writerSettings);
    }
}
