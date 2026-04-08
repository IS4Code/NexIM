using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xrd.Protocol.Handlers;

/// <inheritdoc/>
public partial class CapturingHandler<THandler> : BaseCapturingHandler<THandler>, IPayloadHandler where THandler : IPayloadHandler
{
    protected virtual CapturingHandler<TNewHandler> ForkInner<TNewHandler>() where TNewHandler : IPayloadHandler
    {
        return new CapturingHandler<TNewHandler>();
    }

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        var container = await payloadReader.CaptureContent();
        Capture<IPayloadHandler>(handler => container.RestoreContent(handler.Other));
    }
}
