using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Xmpp.Protocol.Handlers;

/// <summary>
/// Provides a handler that captures all its method calls for later playback.
/// </summary>
/// <typeparam name="THandler">
/// The supported type of the handler.
/// </typeparam>
public partial class CapturingHandler<THandler> : IPayloadHandler, ICapturingHandler<THandler> where THandler : IPayloadHandler
{
    readonly List<Func<THandler, ValueTask>> calls = new();
    bool disposed;

    public IReadOnlyList<Func<THandler, ValueTask>> Calls => calls;

    protected virtual CapturingHandler<TNewHandler> ForkInner<TNewHandler>() where TNewHandler : IPayloadHandler
    {
        return new CapturingHandler<TNewHandler>();
    }

    public async ValueTask Replay(THandler handler)
    {
        foreach(var call in calls)
        {
            await call(handler);
        }
    }

    private void Capture<TImplHandler>(Func<TImplHandler, ValueTask> call)
    {
        if(!disposed && this is ICapturingHandler<TImplHandler> handler)
        {
            handler.Capture(call);
        }
    }

    void ICapturingHandler<THandler>.Capture(Func<THandler, ValueTask> call)
    {
        calls.Add(call);
    }

    ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        return default;
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        return default;
    }
}


interface ICapturingHandler<out THandler>
{
    void Capture(Func<THandler, ValueTask> call);
}
