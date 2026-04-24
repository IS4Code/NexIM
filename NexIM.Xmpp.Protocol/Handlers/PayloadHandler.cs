using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NexIM.Primitives.Xml;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Protocol.Grammar;

namespace NexIM.Xmpp.Protocol.Handlers;

public abstract class PayloadHandler<TContext> : BasePayloadHandler<TContext> where TContext : IPayloadHandlerContext
{
    static readonly ConditionalWeakTable<string, Decoder> fallbackDecoders = new();
    static readonly ConditionalWeakTable<string, Decoder>.CreateValueCallback fallbackDecoderFactory = ns => new FallbackDecoder(ns);

    private protected FallbackEncoder GetEncoder(bool exit)
    {
        return new(this, exit);
    }

    protected async override ValueTask<ValueTask> Decode(XmlReader reader, IPayloadHandler handler)
    {
        bool isEmpty = reader.IsEmptyElement;
        var decoder = fallbackDecoders.GetValue(Context?.DefaultNamespace ?? String.Empty, fallbackDecoderFactory);
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

    private sealed class FallbackDecoder(string defaultNs) : Decoder
    {
        public override string GetDefaultNamespace(XmlNameTable nameTable)
        {
            return nameTable.Add(defaultNs);
        }
    }

    private protected sealed class FallbackEncoder : Encoder, IStreamHandler
    {
        readonly PayloadHandler<TContext> parent;
        readonly bool exit;
        readonly XElement container;

        public override string? DefaultNamespace => parent.Context?.DefaultNamespace;

        protected override CancellationToken CancellationToken => default;
        protected override XmlWriter Writer { get; }

        public FallbackEncoder(PayloadHandler<TContext> parent, bool exit)
        {
            this.parent = parent;
            this.exit = exit;
            container = new XElement("_");

            // Everything will be stored as nodes in the container
            Writer = container.CreateWriter().WithAsyncSupport();
        }

        protected override ValueTask<Encoder> ForkInner()
        {
            // Exactly one fork will happen from the decoder
            return new(new Forked(this));
        }

        public async override ValueTask DisposeAsync()
        {
            Writer.Dispose();
            try
            {
                foreach(var element in container.Elements())
                {
                    using var reader = element.CreateReader().WithAsyncSupport();
                    await reader.ReadAsync();

                    // Present the element to the handler (directly because this is already a fallback)
                    var task = parent.OnOther(reader);
                    if(!task.Equals(DefaultImplementation.ValueTask))
                    {
                        await task;
                    }
                    else
                    {
                        await parent.OnUnrecognized(reader);
                    }
                }
            }
            finally
            {
                container.RemoveAll();
                if(exit)
                {
                    await parent.OnExit();
                }
            }
        }

        sealed class Forked(FallbackEncoder encoder) : Encoder
        {
            public override string? DefaultNamespace => encoder.DefaultNamespace;
            protected override CancellationToken CancellationToken => default;
            protected override XmlWriter Writer => encoder.Writer;

            protected override ValueTask<Encoder> ForkInner()
            {
                return new(new Nested(encoder));
            }

            public async override ValueTask DisposeAsync()
            {
                // Finalize element
                await base.DisposeAsync();
                await encoder.DisposeAsync();
            }

            sealed class Nested(FallbackEncoder encoder) : Encoder
            {
                public override string? DefaultNamespace => encoder.DefaultNamespace;
                protected override CancellationToken CancellationToken => default;
                protected override XmlWriter Writer => encoder.Writer;

                protected override ValueTask<Encoder> ForkInner()
                {
                    return new(this);
                }
            }
        }
    }
}
