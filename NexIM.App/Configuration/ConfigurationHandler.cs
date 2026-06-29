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
using NexIM.Server.Security;
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

sealed class ConfigurationHandler : BaseHandler, IServerHandler, IHttpHandler, IDatabaseHandler, ITlsHandler, IXmppHandler
{
    public DatabaseType? DatabaseType { get; private set; }
    public string ConnectionString { get; private set; } = "";
    public XmppServerReceiver XmppReceiver { get; } = new();
    public XmppTcpListener? XmppTcp { get; private set; }
    public XmppWebSocketListener? XmppWebSocket { get; private set; }
    public XmppWebServer? XmppHtml { get; private set; }
    public WellKnownServices? Metadata { get; private set; }
    public CertificateManager? CertificateManager { get; private set; }
    public List<CertificateSource>? CertificateSources { get; private set; }

    async ValueTask<IHttpHandler> IServerHandler.Http() => this;
    async ValueTask<ITlsHandler> IServerHandler.Tls() => this;
    async ValueTask<IXmppHandler> IServerHandler.Xmpp() => this;

    internal async ValueTask ReadFrom(XmlReader reader)
    {
        while(reader.NodeType != XmlNodeType.None)
        {
            // Keep decoding while there is content
            await await Decode(reader, this);
        }

        // Finish configuration
        if(CertificateSources is { Count: > 0 } sources)
        {
            CertificateManager = new(sources);

            // Register certificate-taking services
            CertificateManager.Register(XmppTcp);
            CertificateManager.Register(XmppWebSocket);
            CertificateManager.Register(XmppHtml);
            CertificateManager.Register(Metadata);
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

    async ValueTask<IDatabaseHandler> IServerHandler.Database(Token<DatabaseType>? type)
    {
        DatabaseType = type?.ToEnum() ?? throw new ApplicationException($"Database type '{type?.Value}' is not recognized.");
        return this;
    }

    async ValueTask IDatabaseHandler.ConnectionString(string? configString)
    {
        ConnectionString = configString ?? "";
    }

    async ValueTask IHttpHandler.ManagedListener(bool? isManaged)
    {
        Server.Configuration.HttpListenerIsManaged = isManaged ?? true;
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

    async ValueTask<ICertificateHandler> ITlsHandler.Certificate(Token<CertificateType>? type)
    {
        var sources = CertificateSources ??= new();
        return type?.ToEnum() switch {
            CertificateType.SelfSigned => new SelfSignedHandler(type, sources),
            CertificateType.File => new FileHandler(type, sources),
            CertificateType.Store => new StoreHandler(type, sources),
            CertificateType.Pem => new PemHandler(type, sources),
            CertificateType.Pkcs12 => new Pkcs12Handler(type, sources),
            _ => throw new ApplicationException($"Certificate type '{type?.Value}' is not recognized.")
        };
    }

    abstract class CertificateHandler(Token<CertificateType>? type, List<CertificateSource> sources) : BaseHandler, ICertificateHandler
    {
        TimeSpan? refreshDelay;
        protected TimeSpan RefreshDelay => refreshDelay ?? TimeSpan.FromDays(1);

        protected void NotSupported(string elementName)
        {
            throw new ApplicationException($"The '{elementName}' element is not supported for a certificate of type '{type?.Value}'.");
        }

        protected T Missing<T>(string elementName)
        {
            throw new ApplicationException($"The '{elementName}' element is required for a certificate of type '{type?.Value}'.");
        }

        public async virtual ValueTask Endpoint(IReadOnlyList<string>? endpoints)
        {
            NotSupported("Endpoint");
        }

        public async virtual ValueTask Issued(TimeSpan? duration)
        {
            NotSupported("Issued");
        }

        public async virtual ValueTask Expires(TimeSpan? duration)
        {
            NotSupported("Expires");
        }

        public async virtual ValueTask SubjectName(string? value)
        {
            NotSupported("SubjectName");
        }

        public async virtual ValueTask StoreName(string? value)
        {
            NotSupported("StoreName");
        }

        public async virtual ValueTask StoreLocation(string? value)
        {
            NotSupported("StoreLocation");
        }

        public async virtual ValueTask CertificatePath(string? path)
        {
            NotSupported("CertificatePath");
        }

        public async virtual ValueTask KeyPath(string? path)
        {
            NotSupported("KeyPath");
        }

        public async virtual ValueTask Password(TemporaryString? password)
        {
            NotSupported("Password");
        }

        public async ValueTask RefreshAfter(TimeSpan? duration)
        {
            refreshDelay = duration;
        }

        protected abstract CertificateSource CreateSource();

        public async override ValueTask DisposeAsync()
        {
            sources.Add(CreateSource());
        }
    }

    sealed class SelfSignedHandler(Token<CertificateType>? type, List<CertificateSource> sources) : CertificateHandler(type, sources)
    {
        string? subjectName;
        TimeSpan? issued, expires;
        HashSet<EndPoint>? endPoints;

        public async override ValueTask SubjectName(string? value)
        {
            subjectName = value;
        }

        public async override ValueTask Issued(TimeSpan? duration)
        {
            issued = duration;
        }

        public async override ValueTask Expires(TimeSpan? duration)
        {
            expires = duration;
        }

        public async override ValueTask Endpoint(IReadOnlyList<string>? endpoints)
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

        protected override CertificateSource CreateSource()
        {
            return new CertificateSource.SelfSigned(
                subjectName ?? Missing<string>("SubjectName"),
                issued ?? TimeSpan.Zero,
                expires ?? TimeSpan.FromDays(7),
                endPoints
            ) {
                RefreshDelay = RefreshDelay
            };
        }
    }

    sealed class StoreHandler(Token<CertificateType>? type, List<CertificateSource> sources) : CertificateHandler(type, sources)
    {
        StoreName? storeName;
        StoreLocation? storeLocation;
        string? subjectName;

        public async override ValueTask SubjectName(string? value)
        {
            subjectName = value;
        }

        public async override ValueTask StoreName(string? value)
        {
            storeName = Enum.Parse<StoreName>(value ?? "");
        }

        public async override ValueTask StoreLocation(string? value)
        {
            storeLocation = Enum.Parse<StoreLocation>(value ?? "");
        }

        protected override CertificateSource CreateSource()
        {
            return new CertificateSource.FromStore(
                storeName ?? Missing<StoreName>("StoreName"),
                storeLocation ?? Missing<StoreLocation>("StoreLocation"),
                subjectName ?? Missing<string>("SubjectName")
            ) {
                RefreshDelay = RefreshDelay
            };
        }
    }

    sealed class FileHandler(Token<CertificateType>? type, List<CertificateSource> sources) : CertificateHandler(type, sources)
    {
        string? path;

        public async override ValueTask CertificatePath(string? path)
        {
            this.path = path;
        }

        protected override CertificateSource CreateSource()
        {
            return new CertificateSource.FromFile(
                path ?? Missing<string>("CertificatePath")
            ) {
                RefreshDelay = RefreshDelay
            };
        }
    }

    sealed class Pkcs12Handler(Token<CertificateType>? type, List<CertificateSource> sources) : CertificateHandler(type, sources)
    {
        string? path;
        TemporaryString? password;

        public async override ValueTask CertificatePath(string? path)
        {
            this.path = path;
        }

        public async override ValueTask Password(TemporaryString? password)
        {
            this.password = TemporaryString.MoveFrom(password);
        }

        protected override CertificateSource CreateSource()
        {
            return new CertificateSource.FromPkcs12File(
                path ?? Missing<string>("CertificatePath"),
                password ?? Missing<TemporaryString>("Password")
            ) {
                RefreshDelay = RefreshDelay
            };
        }
    }

    sealed class PemHandler(Token<CertificateType>? type, List<CertificateSource> sources) : CertificateHandler(type, sources)
    {
        string? path, keyPath;
        TemporaryString? password;

        public async override ValueTask CertificatePath(string? path)
        {
            this.path = path;
        }

        public async override ValueTask KeyPath(string? path)
        {
            keyPath = path;
        }

        public async override ValueTask Password(TemporaryString? password)
        {
            this.password = TemporaryString.MoveFrom(password);
        }

        protected override CertificateSource CreateSource()
        {
            return new CertificateSource.FromPemFile(
                path ?? Missing<string>("CertificatePath"),
                keyPath,
                password
            ) {
                RefreshDelay = RefreshDelay
            };
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
