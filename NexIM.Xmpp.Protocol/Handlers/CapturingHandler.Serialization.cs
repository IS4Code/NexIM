using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NexIM.Primitives.Events;
using NexIM.Primitives.Xml;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol.Handlers;

partial class CapturingHandler<THandler>
{
    static readonly XmlWriterSettings writerSettings = new() {
        Async = false, // use non-async stream writing
        CheckCharacters = false,
        CloseOutput = false,
        ConformanceLevel = ConformanceLevel.Document,
        Encoding = new UTF8Encoding(false),
        Indent = false,
        NamespaceHandling = NamespaceHandling.OmitDuplicates,
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Entitize,
        OmitXmlDeclaration = true,
        NewLineOnAttributes = false,
        WriteEndDocumentOnClose = false
    };

    static readonly XElement defaultContainer = new XText("").CreateReader().CaptureContent().AsTask().Result;

    ReadOnlySequence<byte> IEventExtension.Serialize()
    {
        switch(Calls.Count)
        {
            case 0:
                return default;

            case 1 when Calls[0].Target is XmlClosure { Container: var container }:
                // Single element to serialize
                return SerializeContainer(container);
        }

        // Create a compound container

        foreach(var call in Calls)
        {
            if(call.Target is XmlClosure { Container: var container })
            {
                // Reuse the container attributes
                var newContainer = new XElement(container.Name, container.Attributes());
                return SerializeInto(newContainer);
            }
        }

        // Use the default container

        var blankContainer = new XElement(defaultContainer);
        blankContainer.RemoveNodes();
        return SerializeInto(blankContainer);
    }

    ReadOnlySequence<byte> SerializeInto(XElement container)
    {
        CapturingEncoder? encoder = null;

        foreach(var call in Calls)
        {
            if(call.Target is XmlClosure { Container: var callContainer })
            {
                // Copy all child nodes
                container.Add(callContainer.Nodes());
            }
            else if((encoder ??= new(container.CreateWriter().WithAsyncSupport())) is THandler handler)
            {
                var task = call(handler);
                if(!task.IsCompletedSuccessfully)
                {
                    // Should be synchronous
                    task.AsTask().GetAwaiter().GetResult();
                }
            }
        }

        return SerializeContainer(container);
    }

    private static ReadOnlySequence<byte> SerializeContainer(XElement container)
    {
        using var stream = new MemoryStream();
        using(var writer = XmlWriter.Create(stream, writerSettings))
        {
            container.WriteTo(writer);
        }
        if(!stream.TryGetBuffer(out var buffer))
        {
            buffer = new(stream.ToArray());
        }
        return new(buffer.AsMemory());
    }

    class XmlClosure(XElement container)
    {
        public XElement Container => container;

        public ValueTask Restore(IPayloadHandler handler)
        {
            return container.RestoreContent(handler.Other);
        }
    }

    class CapturingEncoder(XmlWriter writer) : Grammar.Encoder
    {
        int level = 1;

        public override string? DefaultNamespace => null;

        protected override CancellationToken CancellationToken => default;

        protected override XmlWriter Writer => writer;

        protected override ValueTask<Grammar.Encoder> ForkInner()
        {
            // Reuse the current instance to encode nested elements
            if(Interlocked.Increment(ref level) <= 1)
            {
                throw new ObjectDisposedException(ToString());
            }
            return new(this);
        }

        public override ValueTask DisposeAsync()
        {
            if(Interlocked.Decrement(ref level) < 0)
            {
                return default;
            }
            return base.DisposeAsync();
        }
    }
}
