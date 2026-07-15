using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Tools;

internal sealed class PayloadHandlers<THandler> : Stack<THandler>, IDisposable, IAsyncDisposable where THandler : IPayloadHandler
{
    public TDerivedHandler Get<TDerivedHandler>() where TDerivedHandler : THandler
    {
        if(!this.TryPeek(out var top) || top is not TDerivedHandler handler)
        {
            throw new NotSupportedException("The current payload handler does not support this element.");
        }
        return handler;
    }

    public void PopDispose()
    {
        if(this.TryPop(out var top))
        {
            if(top is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public async ValueTask PopDisposeAsync()
    {
        if(this.TryPop(out var top))
        {
            await top.DisposeAsync();
        }
    }

    public void PopDisposeAll()
    {
        while(this.TryPop(out var top))
        {
            if(top is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public void Dispose()
    {
        PopDisposeAll();
    }

    public async ValueTask DisposeAsync()
    {
        while(this.TryPop(out var top))
        {
            bool success = false;
            try
            {
                await top.DisposeAsync();
                success = true;
            }
            finally
            {
                if(!success)
                {
                    await DisposeAsync();
                }
            }
        }
    }
}
