using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using NexIM.Primitives.Json.Handlers;

namespace NexIM.Xrd.Protocol.Grammar;

using Json = Primitives.Json.Handlers;
using Xml = Primitives.Xml.Handlers;

public abstract partial class JsonEncoder : Primitives.Json.JsonEncoder, Xml.IPayloadHandler, Json.IPayloadHandler
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<JsonEncoder> ForkInner();

    ValueTask Xml.IPayloadHandler.Other(XmlReader payloadReader)
    {
        // Not supported
        return default;
    }

    ValueTask IPayloadHandler.Other(JsonReader payloadReader)
    {
        return new(Writer.WriteTokenAsync(payloadReader));
    }

    public virtual ValueTask DisposeAsync()
    {
        // Finalize object
        return new(Writer.WriteEndObjectAsync());
    }
}
