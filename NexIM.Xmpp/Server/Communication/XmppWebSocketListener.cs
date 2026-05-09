using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Metadata;
using NexIM.Primitives;
using NexIM.Server;
using NexIM.Server.Net;
using NexIM.Xrd.Protocol;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Listens to WebSocket XMPP connections.
/// </summary>
public class XmppWebSocketListener : XmppServerListener<(IHttpListenerRequest request, WebSocketContext context), XmppFrameSession>, IMetadataProvider, IMetadataDescriptor
{
    readonly IHttpListener listener;

    public ICollection<string> Prefixes => listener.Prefixes;

    protected override ConformanceLevel ConformanceLevel => ConformanceLevel.Fragment;

    XmppServer Server => (XmppServer)base.Receiver;

    public XmppWebSocketListener(XmppServer server) : base(server)
    {
        listener = Configuration.CreateHttpListener();
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

    protected async void HandleContext(IHttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var wsContext = await context.AcceptWebSocketAsync("xmpp");

            using var socket = wsContext.WebSocket;

            await Start((context.Request, wsContext), cancellationToken);
        }
        catch(Exception e) when(Configuration.OnUnexpectedException(e))
        {

        }
    }

    protected async override ValueTask<XmppFrameSession> CreateSession((IHttpListenerRequest request, WebSocketContext context) info, CancellationToken cancellationToken)
    {
        var request = info.request;
        var wrapper = new Request(request, request.IsSecureConnection ? await request.GetClientCertificateAsync() : null);
        return new XmppWebSocketSession(Server, wrapper, info.context, ReaderSettings, WriterSettings, cancellationToken);
    }

    ValueTask<IMetadataDescriptor?> IMetadataProvider.GetHostDescriptor(Uri uri)
    {
        return new(this);
    }

    ValueTask IMetadataDescriptor.Properties(Uri uri, IResourceDescriptorHandler handler)
    {
        return default;
    }

    static readonly Regex prefixRegex = new(@"^http(s?)://([^:/]*)(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public IEnumerable<string> GetEndpoints(Uri uri)
    {
        foreach(var prefix in Prefixes)
        {
            if(prefixRegex.Match(prefix) is not { Success: true } match)
            {
                continue;
            }

            var host = match.Groups[2].Value;
            if(host is "+" or "*")
            {
                // Works for any host, so pick whichever was used
                host = uri.Host;
            }

            var href = $"ws{match.Groups[1].Value}://{host}{match.Groups[3].Value}";

            yield return href;
        }
    }

    async ValueTask IMetadataDescriptor.Links(Uri uri, IResourceDescriptorHandler handler)
    {
        foreach(var href in GetEndpoints(uri))
        {
            await using var link = await handler.Link(LinkRelation.WebSocketConnection.ToToken(), null, ValueUri.Parse(href), null);
        }
    }

    class Request(IHttpListenerRequest request, X509Certificate? certificate) : IWebSocketRequest
    {
        public X509Certificate? RemoteCertificate => certificate;
        public EndPoint LocalEndPoint => request.LocalEndPoint;
        public EndPoint RemoteEndPoint => request.RemoteEndPoint;
    }
}
