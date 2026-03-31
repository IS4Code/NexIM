using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Server.Communication;

/// <summary>
/// Listens to WebSocket XMPP connections using a managed implementation.
/// </summary>
public class XmppManagedWebSocketListener : XmppServerListener<vtortola.WebSockets.WebSocket, XmppFrameSession>
{
    static readonly string[] protocols = { "xmpp" };

    readonly HashSet<string> prefixes = new();

    public ICollection<string> Prefixes => prefixes;

    XmppServer Server => (XmppServer)base.Receiver;

    public XmppManagedWebSocketListener(XmppServer server) : base(server)
    {

    }

    public async override Task RunAsync(CancellationToken cancellationToken = default)
    {
        var options = new vtortola.WebSockets.WebSocketListenerOptions()
        {
            SubProtocols = protocols
        };
        vtortola.WebSockets.Rfc6455.WebSocketFactoryCollectionExtensions.RegisterRfc6455(options.Standards);

        var listener = new vtortola.WebSockets.WebSocketListener(prefixes.Select(prefix => new Uri(prefix)).ToArray(), options);

        await listener.StartAsync();
        try
        {
            cancellationToken.Register(() => listener.StopAsync().GetAwaiter().GetResult());

            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(await listener.AcceptWebSocketAsync(cancellationToken) is { } webSocket)
                {
                    HandleRequest(webSocket, cancellationToken);
                }
            }
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    protected async void HandleRequest(vtortola.WebSockets.WebSocket webSocket, CancellationToken cancellationToken)
    {
        try
        {
            using var socket = webSocket;

            await Start(socket, cancellationToken);
        }
        catch(Exception e) when(Program.SuppressUnexpectedExceptions())
        {
            Console.WriteLine(e);
        }
    }

    protected override ValueTask<XmppFrameSession> CreateSession(vtortola.WebSockets.WebSocket webSocket, CancellationToken cancellationToken)
    {
        var context = new Context(webSocket);
        return new(new XmppWebSocketSession(Server, context, context, ReaderSettings, WriterSettings, cancellationToken));
    }

    sealed class Context(vtortola.WebSockets.WebSocket webSocket) : WebSocketContext, IWebSocketRequest
    {
        readonly Socket socket = new(webSocket);

        public override CookieCollection CookieCollection => throw new NotImplementedException();

        public override NameValueCollection Headers => throw new NotImplementedException();

        public override bool IsAuthenticated => throw new NotImplementedException();

        public override bool IsLocal => webSocket.LocalEndpoint.SameAddressAs(webSocket.RemoteEndpoint);

        public override bool IsSecureConnection => webSocket.HttpRequest.IsSecure;

        public override string Origin => throw new NotImplementedException();

        public override Uri RequestUri => webSocket.HttpRequest.RequestUri;

        public override string SecWebSocketKey => throw new NotImplementedException();

        public override IEnumerable<string> SecWebSocketProtocols => throw new NotImplementedException();

        public override string SecWebSocketVersion => throw new NotImplementedException();

        public override IPrincipal? User => throw new NotImplementedException();

        public override WebSocket WebSocket => socket;

        X509Certificate? IWebSocketRequest.RemoteCertificate => null; // TODO Not supported

        EndPoint IWebSocketRequest.LocalEndPoint => webSocket.LocalEndpoint;

        EndPoint IWebSocketRequest.RemoteEndPoint => webSocket.RemoteEndpoint;

        sealed class Socket(vtortola.WebSockets.WebSocket webSocket) : WebSocket
        {
            vtortola.WebSockets.WebSocketMessageWriteStream? writer;
            vtortola.WebSockets.WebSocketMessageReadStream? reader;

            public override WebSocketCloseStatus? CloseStatus => (WebSocketCloseStatus?)webSocket.CloseReason;

            public override string? CloseStatusDescription => null;

            public override WebSocketState State => webSocket.IsConnected ? WebSocketState.Open : WebSocketState.Closed;

            public override string? SubProtocol => webSocket.SubProtocol;

            public override void Abort()
            {
                throw new NotImplementedException();
            }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                return webSocket.CloseAsync((vtortola.WebSockets.WebSocketCloseReason)closeStatus);
            }

            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                return webSocket.CloseAsync((vtortola.WebSockets.WebSocketCloseReason)closeStatus);
            }

            public override void Dispose()
            {
                webSocket.Dispose();
            }

            private async ValueTask<vtortola.WebSockets.WebSocketMessageReadStream> CreateReader(CancellationToken cancellationToken)
            {
                if(reader == null)
                {
                    reader = await webSocket.ReadMessageAsync(cancellationToken);
                }
                return reader;
            }

            public async override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                while(true)
                {
                    var reader = await CreateReader(cancellationToken);
                    if(reader == null)
                    {
                        return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                    }
                    var read = await reader.ReadAsync(buffer.Array, buffer.Offset, buffer.Count, cancellationToken);
                    if(read == 0 && buffer.Count != 0)
                    {
                        await reader.DisposeAsync();
                        this.reader = null;
                        continue;
                    }
                    return new WebSocketReceiveResult(read, GetMessageType(reader.MessageType), read == 0);
                }
            }

            public async override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                while(true)
                {
                    var reader = await CreateReader(cancellationToken);
                    if(reader == null)
                    {
                        return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                    }
                    var read = await reader.ReadAsync(buffer, cancellationToken);
                    if(read == 0 && buffer.Length != 0)
                    {
                        await reader.DisposeAsync();
                        this.reader = null;
                        continue;
                    }
                    return new ValueWebSocketReceiveResult(read, GetMessageType(reader.MessageType), read == 0);
                }
            }

            private WebSocketMessageType GetMessageType(vtortola.WebSockets.WebSocketMessageType messageType)
            {
                return messageType switch {
                    vtortola.WebSockets.WebSocketMessageType.Binary => WebSocketMessageType.Binary,
                    vtortola.WebSockets.WebSocketMessageType.Text => WebSocketMessageType.Text,
                };
            }

            private vtortola.WebSockets.WebSocketMessageWriteStream CreateWriter(WebSocketMessageType messageType)
            {
                if(writer == null)
                {
                    writer = webSocket.CreateMessageWriter(
                        messageType switch
                        {
                            WebSocketMessageType.Binary => vtortola.WebSockets.WebSocketMessageType.Binary,
                            WebSocketMessageType.Text => vtortola.WebSockets.WebSocketMessageType.Text
                        }
                    );
                }
                return writer;
            }

            public async override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                var writer = CreateWriter(messageType);
                await writer.WriteAsync(buffer.Array, buffer.Offset, buffer.Count, cancellationToken);
                if(endOfMessage)
                {
                    this.writer = null;
                    try
                    {
                        await writer.CloseAsync();
                    }
                    finally
                    {
                        await writer.DisposeAsync();
                    }
                }
            }

            public async override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                var writer = CreateWriter(messageType);
                await writer.WriteAsync(buffer, cancellationToken);
                if(endOfMessage)
                {
                    this.writer = null;
                    try
                    {
                        await writer.CloseAsync();
                    }
                    finally
                    {
                        await writer.DisposeAsync();
                    }
                }
            }
        }
    }
}
