using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unicord.Primitives.Xml.Handlers;

public abstract class BaseCapturingHandler
{
    internal BaseCapturingHandler()
    {

    }

    internal interface IHandler<out THandler> : IAsyncDisposable
    {
        void Capture(Func<THandler, ValueTask> call);
    }
}

/// <summary>
/// Provides a handler that captures all its method calls for later playback.
/// </summary>
/// <typeparam name="THandler">
/// The supported type of the handler.
/// </typeparam>
public abstract class BaseCapturingHandler<THandler> : BaseCapturingHandler, BaseCapturingHandler.IHandler<THandler>, ICapturingHandler<THandler> where THandler : IPayloadHandler
{
    readonly List<Func<THandler, ValueTask>> calls = new();
    bool disposed;

    public IReadOnlyList<Func<THandler, ValueTask>> Calls => calls;

    public async ValueTask Replay(THandler handler)
    {
        foreach(var call in calls)
        {
            await call(handler);
        }
    }

    protected void Capture<TImplHandler>(Func<TImplHandler, ValueTask> call)
    {
        if(!disposed && this is IHandler<TImplHandler> handler)
        {
            handler.Capture(call);
        }
    }

    void IHandler<THandler>.Capture(Func<THandler, ValueTask> call)
    {
        calls.Add(call);
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        return default;
    }
}

public interface ICapturingHandler<in THandler>
{
    ValueTask Replay(THandler handler);
}
