using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives.Events;
using NexIM.Primitives.Xml;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol.Handlers;

/// <inheritdoc/>
public partial class CapturingHandler<THandler> : BaseCapturingHandler<THandler>, IPayloadHandler, IMixedPayloadHandler, IStreamHandler, IEventExtension where THandler : IPayloadHandler
{
    EventExtensionType IEventExtension.Type => EventExtensionType.Xmpp;

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

    async ValueTask IMixedPayloadHandler.TextContent(XmlReader textReader)
    {
        var text = await textReader.GetValueAsync();
        Capture<IMixedPayloadHandler>(h => h.TextContent(text.AsMemory()));
    }

    ValueTask IMixedPayloadHandler.TextContent(ReadOnlyMemory<char> text)
    {
        if(!MemoryMarshal.TryGetString(text, out _, out _, out _))
        {
            // Preserve
            text = text.ToString().AsMemory();
        }
        Capture<IMixedPayloadHandler>(h => h.TextContent(text));
        return default;
    }

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        var container = await payloadReader.CaptureContent();
        Capture<IPayloadHandler>(new XmlClosure(container).Restore);
    }
}
