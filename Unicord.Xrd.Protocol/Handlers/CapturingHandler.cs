using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unicord.Primitives.Xml;

namespace Unicord.Xrd.Protocol.Handlers;

using Xml = Primitives.Xml.Handlers;
using Json = Primitives.Json.Handlers;

/// <inheritdoc/>
public partial class CapturingHandler<THandler> : Xml.BaseCapturingHandler<THandler>, Xml.IPayloadHandler, Json.IPayloadHandler where THandler : Xml.IPayloadHandler
{
    protected virtual CapturingHandler<TNewHandler> ForkInner<TNewHandler>() where TNewHandler : Xml.IPayloadHandler
    {
        return new CapturingHandler<TNewHandler>();
    }

    async ValueTask Xml.IPayloadHandler.Other(XmlReader payloadReader)
    {
        var container = await payloadReader.CaptureContent();
        Capture<Xml.IPayloadHandler>(handler => container.RestoreContent(handler.Other));
    }

    async ValueTask Json.IPayloadHandler.Other(JsonReader payloadReader)
    {
        var container = await JObject.LoadAsync(payloadReader);
        Capture<Json.IPayloadHandler>(async handler => {
            using var reader = container.CreateReader();
            await handler.Other(reader);
        });
    }
}
