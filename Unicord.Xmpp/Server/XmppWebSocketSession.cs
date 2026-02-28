using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Server;

internal sealed class XmppWebSocketSession(IWebSocketRequest request, WebSocketContext context, XmlReaderSettings readerSettings, XmlWriterSettings writerSettings, CancellationToken cancellationToken) : XmppFrameSession(WebSocketStream.Create(context.WebSocket, WebSocketMessageType.Text, ArrayPool<byte>.Shared))
{
    protected override string DefaultLanguage => "en";

    public override bool Connected => context.WebSocket.State is WebSocketState.Connecting or WebSocketState.Open;

    public override bool IsSecure =>
        context.IsLocal
        || context.IsSecureConnection
        || RemoteEndPoint is IPEndPoint { Address: var addr } && IPAddress.IsLoopback(addr);

    public override bool CanUpgradeTls => false;

    public override bool CanCompress => false;

    public override EndPoint? RemoteEndPoint => request.RemoteEndPoint;

    public override CancellationToken CancellationToken => cancellationToken;

    protected override void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer)
    {
        stream = new ConsoleDebuggingStream(stream);

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

public interface IWebSocketRequest
{
    EndPoint RemoteEndPoint { get; }
}
