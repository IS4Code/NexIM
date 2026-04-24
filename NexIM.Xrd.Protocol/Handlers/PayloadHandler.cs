using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xrd.Protocol.Grammar;

namespace NexIM.Xrd.Protocol.Handlers;

public abstract class PayloadHandler<TContext> : BasePayloadHandler<TContext> where TContext : IPayloadHandlerContext
{
    static readonly Decoder fallbackDecoder = new();

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Called from generated code")]
    private protected NullHandler GetEncoder(bool exit)
    {
        return NullHandler.Instance;
    }

    protected async override ValueTask<ValueTask> Decode(XmlReader reader, IPayloadHandler handler)
    {
        bool isEmpty = reader.IsEmptyElement;
        var decoder = fallbackDecoder;
        var result = await decoder.DecodePayload(reader, handler);

        if(result is (true, var inner))
        {
            // Successfully decoded and called
            if(inner is null or NullHandler)
            {
                // Handler not present or ignored
                return default;
            }
            // A new handler was obtained
            return Inner();
            async ValueTask Inner()
            {
                try
                {
                    if(isEmpty)
                    {
                        // No contents
                        return;
                    }

                    while(await reader.ReadAsync())
                    {
                        if(reader.NodeType == XmlNodeType.Element)
                        {
                            // New element - decode recursively
                            using var subtreeReader = reader.ReadSubtree();
                            await subtreeReader.ReadAsync();
                            await await Decode(subtreeReader, inner);

                            // Skip if not read
                            while(await subtreeReader.ReadAsync())
                            {
                                await subtreeReader.SkipAsync();
                            }
                        }
                    }
                }
                finally
                {
                    await inner.DisposeAsync();
                }
            }
        }
        else if(handler is PayloadHandler<TContext> topHandler)
        {
            return Inner();
            async ValueTask Inner()
            {
                bool exit = await OnEnter();
                try
                {
                    await topHandler.OnUnrecognized(reader);
                }
                finally
                {
                    if(exit)
                    {
                        await OnExit();
                    }
                }
            }
        }
        else
        {
            return default;
        }
    }

    private protected readonly struct ExitDisposable(PayloadHandler<TContext> instance) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return instance.OnExit();
        }
    }
}
