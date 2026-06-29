using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Tools;
using NexIM.Xmpp.Tools;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Provides a final <see cref="IXmppSession"/> implementation
/// that communicates using WebSocket.
/// </summary>
internal sealed class XmppWebSocketSession(XmppServerReceiver serverReceiver, IWebSocketRequest request, WebSocketContext context, Tools.WebSocketStream wsStream, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, CancellationToken cancellationToken) : XmppFrameSession(wsStream)
{
    public override string DefaultLanguage => "en";

    public override bool Connected => context.WebSocket.State is WebSocketState.Connecting or WebSocketState.Open;

    public override bool IsSecure =>
        context.IsLocal
        || context.IsSecureConnection;

    public override bool CanUpgradeTls => false;

    public override bool CanCompress => false;

    public override XmppServerReceiver ServerReceiver => serverReceiver;
    public override X509Certificate? RemoteCertificate => request.RemoteCertificate;
    public override EndPoint LocalEndPoint => request.LocalEndPoint;
    public override EndPoint RemoteEndPoint => request.RemoteEndPoint;
    public override CancellationToken CancellationToken => cancellationToken;

    public XmppWebSocketSession(XmppServerReceiver serverReceiver, IWebSocketRequest request, WebSocketContext context, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, CancellationToken cancellationToken) : this(serverReceiver, request, context, OpenStream(context), readerSettings, writerSettings, cancellationToken)
    {

    }

    protected override XmlNameTable NameTable => readerSettings.NameTable ?? base.NameTable;

    protected override void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer)
    {
#if DEBUG
        stream = new ConsoleDebuggingStream(stream, RemoteEndPoint);
#endif

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

    public async override ValueTask FlushCommand()
    {
        await base.FlushCommand();
        if(Writer.WriteState is WriteState.Prolog)
        {
            // Only when something was written
            await wsStream.SendAsync();
        }
    }

    static Tools.WebSocketStream OpenStream(WebSocketContext context)
    {
        return Tools.WebSocketStream.Create(context.WebSocket, WebSocketMessageType.Text, ArrayPool<byte>.Shared);
    }
}

public interface IWebSocketRequest
{
    X509Certificate? RemoteCertificate { get; }
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
}
