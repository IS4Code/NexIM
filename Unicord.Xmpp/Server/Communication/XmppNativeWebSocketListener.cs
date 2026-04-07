using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Server.Communication;

/// <summary>
/// Listens to WebSocket XMPP connections using a native implementation.
/// </summary>
public class XmppNativeWebSocketListener : XmppWebSocketListener<(HttpListenerRequest request, WebSocketContext context)>
{
    readonly HttpListener listener;

    public override ICollection<string> Prefixes => listener.Prefixes;

    XmppServer Server => (XmppServer)base.Receiver;

    public XmppNativeWebSocketListener(XmppServer server) : base(server)
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

            await Start((context.Request, wsContext), cancellationToken);
        }
        catch(Exception e) when(Program.OnUnexpectedException(e))
        {

        }
    }

    protected async override ValueTask<XmppFrameSession> CreateSession((HttpListenerRequest request, WebSocketContext context) info, CancellationToken cancellationToken)
    {
        var request = info.request;
        var wrapper = new Request(request, request.IsSecureConnection ? await request.GetClientCertificateAsync() : null);
        return new XmppWebSocketSession(Server, wrapper, info.context, ReaderSettings, WriterSettings, cancellationToken);
    }

    class Request(HttpListenerRequest request, X509Certificate? certificate) : IWebSocketRequest
    {
        public X509Certificate? RemoteCertificate => certificate;
        public EndPoint LocalEndPoint => request.LocalEndPoint;
        public EndPoint RemoteEndPoint => request.RemoteEndPoint;
    }
}
