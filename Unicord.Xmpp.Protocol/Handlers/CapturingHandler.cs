using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xmpp.Protocol.Handlers;

/// <inheritdoc/>
public partial class CapturingHandler<THandler> : BaseCapturingHandler<THandler>, IPayloadHandler, IStreamHandler where THandler : IPayloadHandler
{
    protected virtual CapturingHandler<TNewHandler> ForkInner<TNewHandler>() where TNewHandler : IPayloadHandler
    {
        return new CapturingHandler<TNewHandler>();
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        var copy = stanza;
        var inner = ForkInner<IInfoQueryHandler>();
        Capture<IStreamHandler>(async h => {
            await using var handler = await h.InfoQuery(copy);
            await inner.Replay(handler);
        });
        return new(inner);
    }

    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        var copy = stanza;
        var inner = ForkInner<IMessageHandler>();
        Capture<IStreamHandler>(async h => {
            await using var handler = await h.Message(copy);
            await inner.Replay(handler);
        });
        return new(inner);
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        var copy = stanza;
        var inner = ForkInner<IPresenceHandler>();
        Capture<IStreamHandler>(async h => {
            await using var handler = await h.Presence(copy);
            await inner.Replay(handler);
        });
        return new(inner);
    }

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        var container = await payloadReader.CaptureContent();
        Capture<IPayloadHandler>(handler => container.RestoreContent(handler.Other));
    }
}
