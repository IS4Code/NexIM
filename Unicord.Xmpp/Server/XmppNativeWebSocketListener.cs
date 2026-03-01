using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class XmppNativeWebSocketListener : XmppFrameListener<(HttpListenerRequest request, WebSocketContext context)>
{
    readonly HttpListener listener;

    public ICollection<string> Prefixes => listener.Prefixes;

    public XmppNativeWebSocketListener(IXmppReceiver<XmppFrameSession> receiver) : base(receiver)
    {
        listener = new();
    }

    public async override Task RunAsync(CancellationToken cancellationToken = default)
    {
        listener.Start();
        try
        {
            cancellationToken.Register(listener.Stop);

            while(await listener.GetContextAsync() is { } context)
            {
                HandleContext(context, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    protected async void HandleContext(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var wsContext = await context.AcceptWebSocketAsync("xmpp");

            using var socket = wsContext.WebSocket;

            await HandleSocket((context.Request, wsContext), cancellationToken);
        }
        catch(Exception e) when(!Debugger.IsAttached)
        {
            Console.WriteLine(e);
        }
    }

    protected override ValueTask<XmppFrameSession> StartSession((HttpListenerRequest request, WebSocketContext context) info, CancellationToken cancellationToken)
    {
        return new(new XmppWebSocketSession(new Request(info.request), info.context, ReaderSettings, WriterSettings, cancellationToken));
    }

    class Request(HttpListenerRequest request) : IWebSocketRequest
    {
        public EndPoint LocalEndPoint => request.LocalEndPoint;
        public EndPoint RemoteEndPoint => request.RemoteEndPoint;
    }
}
