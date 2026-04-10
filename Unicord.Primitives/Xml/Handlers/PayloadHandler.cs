using System;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Primitives.Xml.Handlers;

public interface IPayloadHandler : IAsyncDisposable
{
    ValueTask Other(XmlReader payloadReader);
}

public interface IPayloadHandler<TContext> : IPayloadHandler where TContext : IPayloadHandlerContext
{
    TContext? Context { get; init; }
}

public interface IPayloadHandlerContext
{
    string DefaultNamespace { get; }
}

public readonly record struct EmptyPayloadHandlerContext() : IPayloadHandlerContext
{
    public string DefaultNamespace => String.Empty;
}

public abstract class BasePayloadHandler<TContext> : IPayloadHandler<TContext> where TContext : IPayloadHandlerContext
{
    protected bool Decoding { get; private set; }

    public virtual TContext? Context { get; init; }

    protected abstract ValueTask OnUnrecognized(XmlReader payloadReader);

    protected virtual ValueTask OnOther(XmlReader payloadReader)
    {
        return DefaultImplementation.ValueTask;
    }

    protected virtual ValueTask<bool> OnEnter() => default;
    protected virtual ValueTask OnExit() => default;

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        bool exit = !Decoding && await OnEnter();
        try
        {
            var task = OnOther(payloadReader);
            if(!task.Equals(DefaultImplementation.ValueTask))
            {
                // Successfully handled
                await task;
                return;
            }
        }
        finally
        {
            if(exit)
            {
                await OnExit();
            }
        }

        if(Decoding)
        {
            // Called recursively without being handled
            return;
        }

        ValueTask result;

        Decoding = true;
        try
        {
            // Prevent recursion in concrete methods
            result = await Decode(payloadReader, this);
        }
        finally
        {
            Decoding = false;
        }

        // Wait for inner handlers
        await result;
    }

    protected abstract ValueTask<ValueTask> Decode(XmlReader reader, IPayloadHandler handler);

    public abstract ValueTask DisposeAsync();
}
