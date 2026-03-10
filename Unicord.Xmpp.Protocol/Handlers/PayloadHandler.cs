using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol.Grammar;

namespace Unicord.Xmpp.Protocol.Handlers;

public interface IPayloadHandler<TContext> : IPayloadHandler where TContext : IPayloadHandlerContext
{
    TContext? Context { get; init; }
}

public abstract class PayloadHandler<TContext> : IPayloadHandler<TContext> where TContext : IPayloadHandlerContext
{
    private protected bool Decoding { get; private set; }

    public virtual TContext? Context { get; init; }

    protected internal abstract ValueTask OnUnrecognized(XmlReader payloadReader);

    protected internal virtual ValueTask<bool> OnOther(XmlReader payloadReader)
    {
        return default;
    }

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        if(await OnOther(payloadReader))
        {
            // Successfully handled
            return;
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

    static readonly ConditionalWeakTable<string, Decoder> fallbackDecoders = new();
    static readonly ConditionalWeakTable<string, Decoder>.CreateValueCallback fallbackDecoderFactory = ns => new FallbackDecoder(ns);

    private protected FallbackEncoder GetEncoder()
    {
        return new(this);
    }

    private async ValueTask<ValueTask> Decode(XmlReader reader, IPayloadHandler handler)
    {
        bool isEmpty = reader.IsEmptyElement;
        var decoder = fallbackDecoders.GetValue(Context.DefaultNamespace, fallbackDecoderFactory);
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
            return topHandler.OnUnrecognized(reader);
        }
        else
        {
            return default;
        }
    }

    public abstract ValueTask DisposeAsync();

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
        readonly XElement container;

        public override string? DefaultNamespace => parent.Context?.DefaultNamespace;

        protected override CancellationToken CancellationToken => default;
        protected override XmlWriter Writer { get; }

        public FallbackEncoder(PayloadHandler<TContext> parent)
        {
            this.parent = parent;
            container = new XElement("_");

            // Everything will be stored as nodes in the container
            Writer = container.CreateWriter().WithAsyncSupport();
        }

        ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
        {
            var copy = stanza;
            return Inner();
            async ValueTask<IMessageHandler> Inner()
            {
                await WriteStanza(StanzaKind.Message.ToToken(), copy);
                return await ForkInner();
            }
        }

        ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
        {
            var copy = stanza;
            return Inner();
            async ValueTask<IPresenceHandler> Inner()
            {
                await WriteStanza(StanzaKind.Presence.ToToken(), copy);
                return await ForkInner();
            }
        }

        ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
        {
            var copy = stanza;
            return Inner();
            async ValueTask<IInfoQueryHandler> Inner()
            {
                await WriteStanza(StanzaKind.InfoQuery.ToToken(), copy);
                return await ForkInner();
            }
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
                    if(!await parent.OnOther(reader))
                    {
                        await parent.OnUnrecognized(reader);
                    }
                }
            }
            finally
            {
                container.RemoveAll();
            }
        }

        sealed class Forked(FallbackEncoder encoder) : Encoder
        {
            public override string DefaultNamespace => encoder.DefaultNamespace;
            protected override CancellationToken CancellationToken => default;
            protected override XmlWriter Writer => encoder.Writer;

            protected override ValueTask<Encoder> ForkInner()
            {
                return new(new Nested(encoder));
            }

            public async override ValueTask DisposeAsync()
            {
                // Finalize element
                await Writer.WriteEndElementAsync();
                await encoder.DisposeAsync();
            }

            sealed class Nested(FallbackEncoder encoder) : Encoder
            {
                public override string DefaultNamespace => encoder.DefaultNamespace;
                protected override CancellationToken CancellationToken => default;
                protected override XmlWriter Writer => encoder.Writer;

                protected override ValueTask<Encoder> ForkInner()
                {
                    return new(this);
                }

                public override ValueTask DisposeAsync()
                {
                    // Finalize element
                    return new(Writer.WriteEndElementAsync());
                }
            }
        }
    }
}
