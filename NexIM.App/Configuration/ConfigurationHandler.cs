using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NexIM.App.Configuration.Handlers;
using NexIM.Metadata;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Server;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.App.Configuration;

abstract class BaseHandler : PayloadHandler<EmptyPayloadHandlerContext>
{
    public override ValueTask DisposeAsync() => default;

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        using var subtree = payloadReader.ReadSubtree();
        var element = await XElement.LoadAsync(subtree, LoadOptions.None, CancellationToken.None);
        throw new ApplicationException($"Unexpected element <{element.Name}>.");
    }
}

sealed class ConfigurationHandler : BaseHandler, IServerHandler, IDatabaseHandler, ICertificatesHandler, IXmppHandler
{
    public string? SQLiteConnectionString { get; private set; }
    public XmppServerReceiver XmppReceiver { get; } = new();
    public XmppTcpListener? XmppTcp { get; private set; }
    public XmppWebSocketListener? XmppWebSocket { get; private set; }
    public XmppWebServer? XmppHtml { get; private set; }
    public WellKnownServices? Metadata { get; private set; }
    public List<X509Certificate2>? Certificates { get; private set; }

    async ValueTask<IDatabaseHandler> IServerHandler.Database() => this;
    async ValueTask<ICertificatesHandler> IServerHandler.Certificates() => this;
    async ValueTask<IXmppHandler> IServerHandler.Xmpp() => this;

    internal async ValueTask ReadFrom(XmlReader reader)
    {
        while(reader.NodeType != XmlNodeType.None)
        {
            // Keep decoding while there is content
            await await Decode(reader, this);
        }

        // Finish configuration

        if(Certificates is { Count: > 0 } certificates)
        {
            // Link primary certificate to HTTP-based services
            var certificate = certificates[0];
            if(XmppHtml != null)
            {
                XmppHtml.Certificate = certificate;
            }
            if(XmppWebSocket != null)
            {
                XmppWebSocket.Certificate = certificate;
            }
            if(Metadata != null)
            {
                Metadata.Certificate = certificate;
            }
        }

        if(XmppHtml != null)
        {
            // Link WebSocket
            XmppHtml.WebSocketListener = XmppWebSocket;
        }

        if(Metadata != null)
        {
            // Add metadata providers
            if(XmppWebSocket != null)
            {
                Metadata.MetadataProviders.Add(XmppWebSocket);
            }
        }
    }

    async ValueTask IDatabaseHandler.SQLite(string? configString)
    {
        SQLiteConnectionString = configString ?? "";
    }

    async ValueTask<IXmppTcpHandler> IXmppHandler.Tcp()
    {
        return new XmppTcpHandler(XmppTcp ??= new(XmppReceiver));
    }

    sealed class XmppTcpHandler(XmppTcpListener server) : BaseHandler, IXmppTcpHandler
    {
        async ValueTask IServiceHandler.Endpoint(IReadOnlyList<string>? endpoints)
        {
            foreach(var endpoint in endpoints ?? Array.Empty<string>())
            {
                server.EndPoints.Add(IPEndPoint.Parse(endpoint));
            }
        }
    }

    async ValueTask<IXmppWebSocketHandler> IXmppHandler.WebSocket()
    {
        return new XmppWebSocketHandler(XmppWebSocket ??= new(XmppReceiver));
    }

    sealed class XmppWebSocketHandler(XmppWebSocketListener server) : BaseHandler, IXmppWebSocketHandler
    {
        async ValueTask IServiceHandler.Endpoint(IReadOnlyList<string>? endpoints)
        {
            foreach(var endpoint in endpoints ?? Array.Empty<string>())
            {
                server.Prefixes.Add(endpoint);
            }
        }
    }

    async ValueTask<IXmppHtmlHandler> IXmppHandler.Html()
    {
        return new XmppHtmlHandler(XmppHtml ??= new());
    }

    sealed class XmppHtmlHandler(XmppWebServer server) : BaseHandler, IXmppHtmlHandler
    {
        async ValueTask IXmppHtmlHandler.Title(string? title)
        {
            server.Title = title ?? "";
        }

        async ValueTask IXmppHtmlHandler.Language(LanguageCode? language)
        {
            server.Language = language ?? default;
        }

        async ValueTask IXmppHtmlHandler.ConverseDistribution(string? url)
        {
            server.Converse = url ?? "";
        }

        async ValueTask IServiceHandler.Endpoint(IReadOnlyList<string>? endpoints)
        {
            foreach(var endpoint in endpoints ?? Array.Empty<string>())
            {
                server.Prefixes.Add(endpoint);
            }
        }
    }

    async ValueTask<ISelfSignedHandler> ICertificatesHandler.SelfSigned()
    {
        return new SelfSignedHandler(Certificates ??= new());
    }

    sealed class SelfSignedHandler(List<X509Certificate2> certificates) : BaseHandler, ISelfSignedHandler
    {
        string? subjectName;
        TimeSpan? expires;
        HashSet<EndPoint>? endPoints;

        async ValueTask ISelfSignedHandler.SubjectName(string? value)
        {
            subjectName = value;
        }

        async ValueTask ISelfSignedHandler.Expires(TimeSpan? duration)
        {
            expires = duration;
        }

        async ValueTask IServiceHandler.Endpoint(IReadOnlyList<string>? endpoints)
        {
            foreach(var endpoint in endpoints ?? Array.Empty<string>())
            {
                if(IPAddress.TryParse(endpoint, out var ip))
                {
                    (endPoints ??= new()).Add(new IPEndPoint(ip, 0));
                }
                else
                {
                    (endPoints ??= new()).Add(new DnsEndPoint(endpoint, 0));
                }
            }
        }

        public async override ValueTask DisposeAsync()
        {
            certificates.Add(Server.Configuration.GetCertificate(subjectName ?? "", expires, endPoints));
        }
    }

    async ValueTask<IMetadataHandler> IServerHandler.Metadata()
    {
        return new MetadataHandler(Metadata ??= new());
    }

    sealed class MetadataHandler(WellKnownServices metadata) : BaseHandler, IMetadataHandler
    {
        async ValueTask IServiceHandler.Endpoint(IReadOnlyList<string>? endpoints)
        {
            foreach(var endpoint in endpoints ?? Array.Empty<string>())
            {
                metadata.Prefixes.Add(endpoint);
            }
        }
    }
}
