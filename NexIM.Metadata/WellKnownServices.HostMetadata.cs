using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using NexIM.Server.Net;
using NexIM.Xrd.Protocol;
using NexIM.Xrd.Protocol.Grammar;

namespace NexIM.Metadata;

partial class WellKnownServices
{
    public ICollection<IMetadataProvider> MetadataProviders { get; } = new HashSet<IMetadataProvider>();

    static readonly HashSet<string> xrdMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/xrd+xml"
    };

    static readonly HashSet<string> jrdMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/jrd+json"
    };

    static readonly HashSet<string> metaMediaTypes = new(xrdMediaTypes.Concat(jrdMediaTypes), StringComparer.OrdinalIgnoreCase);

    async Task GetHostMeta(IHttpListenerContext context, bool useJson, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        if(request.HttpMethod is not "GET")
        {
            // Ignore other methods
            response.StatusCode = HttpStatusCode.MethodNotAllowed;
            return;
        }

        response.StatusCode = HttpStatusCode.OK;

        var headers = request.Headers;

        // Check explicit accept
        var accept =
            (headers.GetValues("Accept") ?? Array.Empty<string>())
            .Select(MediaTypeWithQualityHeaderValue.Parse)
            .OrderByDescending(h => h.Quality)
            .Select(h => h.MediaType ?? "")
            .FirstOrDefault(metaMediaTypes.Contains);

        if(accept != null)
        {
            if(jrdMediaTypes.Contains(accept))
            {
                useJson = true;
            }
            else if(xrdMediaTypes.Contains(accept))
            {
                useJson = false;
            }
        }

        // Retrieve all descriptors for the host
        var uri = request.Url;
        List<IMetadataDescriptor>? descriptors = null;
        foreach(var provider in MetadataProviders)
        {
            if(await provider.GetHostDescriptor(uri) is not { } descriptor)
            {
                continue;
            }
            (descriptors ??= new()).Add(descriptor);
        }

        // Allow CORS
        response.AddHeader("Access-Control-Allow-Origin", "*");

        if(useJson)
        {
            await OutputJrd(context, descriptors, cancellationToken);
        }
        else
        {
            await OutputXrd(context, descriptors, cancellationToken);
        }
    }

    static readonly XmlWriterSettings xrdWriterSettings = new() {
        Async = true,
        CheckCharacters = false,
        CloseOutput = true,
        WriteEndDocumentOnClose = true,
        ConformanceLevel = ConformanceLevel.Document,
        Indent = false,
        NamespaceHandling = NamespaceHandling.OmitDuplicates,
        NewLineHandling = NewLineHandling.Entitize,
        NewLineOnAttributes = false,
        OmitXmlDeclaration = false
    };

    async Task OutputXrd(IHttpListenerContext context, IEnumerable<IMetadataDescriptor>? descriptors, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        response.ContentType = "application/xrd+xml";

        using var stream = response.OutputStream;
        using var writer = XmlWriter.Create(stream, xrdWriterSettings);

        await writer.WriteStartElementAsync(null, Vocabulary.Standard.Xrd.Value, Vocabulary.Standard.XrdNs.Value);

        var encoder = new XrdEncoder(writer, cancellationToken);

        await OutputDescriptors(request.Url, encoder, descriptors);
    }

    async Task OutputJrd(IHttpListenerContext context, IEnumerable<IMetadataDescriptor>? descriptors, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        response.ContentType = "application/jrd+json";

        using var stream = response.OutputStream;
        using var writer = new StreamWriter(stream);
        using var jsonWriter = new JsonTextWriter(writer);

        await jsonWriter.WriteStartObjectAsync(cancellationToken);

        var encoder = new JrdEncoder(jsonWriter, cancellationToken);

        await OutputDescriptors(request.Url, encoder, descriptors);
    }

    async Task OutputDescriptors(Uri uri, IResourceDescriptorHandler handler, IEnumerable<IMetadataDescriptor>? descriptors)
    {
        if(descriptors != null)
        {
            // Sort by type first
            foreach(var descriptor in descriptors)
            {
                await descriptor.Properties(uri, handler);
            }
            foreach(var descriptor in descriptors)
            {
                await descriptor.Links(uri, handler);
            }
        }
    }

    class XrdEncoder(XmlWriter writer, CancellationToken cancellationToken) : Encoder
    {
        protected override XmlWriter Writer => writer;
        protected override CancellationToken CancellationToken => cancellationToken;

        int level = 0;

        protected override ValueTask<Encoder> ForkInner()
        {
            // Reuse the current instance to encode nested elements
            if(Interlocked.Increment(ref level) < 1)
            {
                throw new ObjectDisposedException(ToString());
            }
            return new(this);
        }

        public override ValueTask DisposeAsync()
        {
            if(Interlocked.Decrement(ref level) < 0)
            {
                return default;
            }
            return base.DisposeAsync();
        }
    }

    class JrdEncoder(JsonWriter writer, CancellationToken cancellationToken) : JsonEncoder
    {
        protected override JsonWriter Writer => writer;
        protected override CancellationToken CancellationToken => cancellationToken;

        int level = 0;

        protected override ValueTask<JsonEncoder> ForkInner()
        {
            // Reuse the current instance to encode nested elements
            if(Interlocked.Increment(ref level) < 1)
            {
                throw new ObjectDisposedException(ToString());
            }
            return new(this);
        }

        public override ValueTask DisposeAsync()
        {
            if(Interlocked.Decrement(ref level) < 0)
            {
                return default;
            }
            return base.DisposeAsync();
        }
    }
}
