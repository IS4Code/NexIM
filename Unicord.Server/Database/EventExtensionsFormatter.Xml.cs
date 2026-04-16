using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Primitives.Events;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Tools;

namespace Unicord.Server.Database;

partial class EventExtensionsFormatter
{
    static readonly XmlReaderSettings readerSettings = new() {
        Async = false, // use non-async stream reading
        CheckCharacters = false,
        CloseInput = false,
        ConformanceLevel = ConformanceLevel.Document,
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        // TODO Shared name table
        ValidationType = ValidationType.None,
        XmlResolver = XmlResolver.ThrowingResolver
    };

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

    /// <remarks>
    /// The handler always calls <see cref="IPayloadHandler.Other(XmlReader)"/>.
    /// Elements and attributes will currently not be recognized,
    /// because the XMPP name table is not used.
    /// </remarks>
    private static XmlReplayHandler DeserializeXml(EventExtensionType type, ReadOnlySequence<byte> data)
    {
        using var stream = new ReadOnlySequenceStream(data);
        using var reader = XmlReader.Create(stream, readerSettings);
        return new(type, (int)data.Length, XElement.Load(reader));
    }

    class XmlReplayHandler(EventExtensionType type, int bufferCapacity, XElement container) : ICapturingHandler<IPayloadHandler>, IEventExtension
    {
        public EventExtensionType Type => type;

        public ValueTask Replay(IPayloadHandler handler)
        {
            return container.RestoreContent(handler.Other);
        }

        public ReadOnlySequence<byte> Serialize()
        {
            // Round-trip serialization should be avoided
            using var stream = new MemoryStream(bufferCapacity);
            using(var writer = XmlWriter.Create(stream, writerSettings))
            {
                container.WriteTo(writer);
            }
            if(!stream.TryGetBuffer(out var buffer))
            {
                buffer = stream.ToArray();
            }
            return new(buffer.AsMemory());
        }
    }
}
