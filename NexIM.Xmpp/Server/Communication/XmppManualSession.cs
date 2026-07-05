using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> 
/// over a manually-created stream.
/// </summary>
public sealed class XmppManualSession(Stream stream, XmppServerReceiver receiver, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, string defaultLanguage = "en") : XmppAuthSession(stream)
{
    public override XmppServerReceiver ServerReceiver => receiver;

    public override bool Connected => true;
    public override bool IsSecure => true;
    public override bool CanUpgradeTls => false;
    public override bool CanCompress => false;

    public override EndPoint? LocalEndPoint => null;
    public override EndPoint? RemoteEndPoint => null;
    public override X509Certificate? RemoteCertificate => null;
    public override CancellationToken CancellationToken => CancellationToken.None;

    public override string DefaultLanguage => defaultLanguage;

    public void Reopen(Stream newStream)
    {
        Initialize(newStream);
    }

    protected override void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer)
    {
        reader = XmlReader.Create(stream, readerSettings);
        writer = XmlWriter.Create(stream, writerSettings);
    }

    protected async override ValueTask EnableCompression()
    {
        throw new NotSupportedException();
    }

    protected async override ValueTask UpgradeTls()
    {
        throw new NotSupportedException();
    }
}
