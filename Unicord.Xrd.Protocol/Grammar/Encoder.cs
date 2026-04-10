using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Unicord.Primitives.Json.Handlers;
using Unicord.Primitives.Xml;

namespace Unicord.Xrd.Protocol.Grammar;

using Xml = Primitives.Xml.Handlers;
using Json = Primitives.Json.Handlers;

public abstract partial class Encoder : XmlEncoder, Xml.IPayloadHandler, Json.IPayloadHandler
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<Encoder> ForkInner();

    public sealed override string DefaultNamespace => String.Empty;

    async ValueTask Xml.IPayloadHandler.Other(XmlReader payloadReader)
    {
        await Writer.WriteNodeWithLanguageAsync(payloadReader, false);
    }

    ValueTask IPayloadHandler.Other(JsonReader payloadReader)
    {
        // Not supported
        return default;
    }

    public virtual ValueTask DisposeAsync()
    {
        // Finalize element
        return new(Writer.WriteEndElementAsync());
    }
}
