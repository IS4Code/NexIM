using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using Xmpp = XmppVocabulary.Standard;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> sending
/// XMPP commands to a <see cref="Stream"/> instance.
/// </summary>
public abstract class XmppStreamSession : XmppHandlerSession
{
    protected Stream Stream { get; private set; }

    XmlWriter writer;
    public sealed override XmlWriter Writer => writer;

    XmlReader reader;
    public sealed override XmlReader Reader => reader;

    public XmppStreamSession(Stream stream)
    {
        Initialize(stream);
    }

    protected abstract void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer);

    [MemberNotNull(nameof(Stream), nameof(writer), nameof(reader))]
    protected void Initialize(Stream stream)
    {
        writer?.Dispose();
        Reader?.Dispose();

        Stream = stream;

        OpenXmlStream(stream, out reader, out writer);
    }

    static readonly byte[] buffer = new byte[1];

    public async override ValueTask<bool> CheckFinished()
    {
        try
        {
            return await Stream.ReadAsync(buffer, 0, 1, CancellationToken) == 0;
        }
        catch
        {
            return true;
        }
    }

    public override ValueTask FlushCommand()
    {
        return new(writer.FlushAsync());
    }

    protected async override ValueTask Close()
    {
        try
        {
            // Stream will be disposed
            await using var stream = Stream;

            // Close for reading and writing
            var task = CloseStream();
            try
            {
                Reader.Dispose();
            }
            finally
            {
                await task;
            }

            async Task CloseStream()
            {
                if(writer.WriteState is not (WriteState.Start or WriteState.Closed))
                {
                    // Data was not closed
                    await EndStream();
                }
                await writer.DisposeAsync();
            }
        }
        catch(Exception e) when(IsAllowedClosingException(e))
        {
            // Accessing closed stream
        }
    }

    protected async override ValueTask Read(CancellationToken cancellationToken)
    {
        var reader = Reader;
        var handler = MainHandler;
        int depth = reader.Depth;
        switch(reader.NodeType)
        {
            case XmlNodeType.XmlDeclaration:
                return;

            case XmlNodeType.Element:
                const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;
                bool isEmpty = reader.IsEmptyElement;
                switch(depth)
                {
                    case 0:
                        // Root stream element
                        if(reader.NamespaceURI != Xmpp.StreamsNs)
                        {
                            throw XmppStreamException.BadNamespacePrefix();
                        }
                        if(reader.LocalName != Xmpp.Stream)
                        {
                            throw XmppStreamException.BadFormat();
                        }

                        var writer = Writer;

                        await writer.WriteStartElementAsync(Xmpp.Stream.Value, Xmpp.Stream.Value, Xmpp.StreamsNs.Value);
                        await writer.WriteAttributeStringAsync(Xmpp.Xmlns.Value, Xmpp.Stream.Value, Xmpp.XmlnsNs.Value, Xmpp.StreamsNs.Value);
                        await writer.WriteAttributeStringAsync(null, Xmpp.Xmlns.Value, null, Xmpp.JabberClientNs.Value);

                        await StreamStarted(reader);

                        // Stream is ready
                        await handler.StreamStarted();
                        break;

                    case 1:
                        // Individual command
                        if(await EnterCommand(reader, handler) is (true, var commandHandler))
                        {
                            // Recognized command type
                            await EnterHandler(commandHandler);
                        }
                        else
                        {
                            // Unknown type
                            using var subtreeReader = reader.ReadSubtree();
                            var element = await XElement.LoadAsync(subtreeReader, elementLoadOptions, cancellationToken);
                            await handler.Other(element);
                        }
                        break;

                    default:
                        // Payload of a known command
                        if(await Decoder.DecodePayload(reader, Handlers.Get<IPayloadHandler>()) is (true, var payloadHandler))
                        {
                            // Recognized payload type
                            await EnterHandler(payloadHandler);
                        }
                        else
                        {
                            // Unknown element
                            using var subtreeReader = reader.ReadSubtree();
                            var element = await XElement.LoadAsync(subtreeReader, elementLoadOptions, cancellationToken);
                            await Handlers.Get<IPayloadHandler>().Other(element);
                        }
                        break;
                }

                ValueTask EnterHandler(IPayloadHandler? handler)
                {
                    if(handler != null)
                    {
                        if(isEmpty)
                        {
                            // No EndElement, close now
                            return handler.DisposeAsync();
                        }
                        else
                        {
                            Handlers.Push(handler);
                        }
                    }
                    return default;
                }
                break;

            case XmlNodeType.EndElement:
                switch(depth)
                {
                    case 0:
                        // End of stream
                        return;
                    default:
                        if(!Handlers.TryPop(out var top))
                        {
                            return;
                        }
                        await top.DisposeAsync();
                        break;
                }
                break;
        }
    }

    protected async virtual ValueTask EndStream()
    {
        if(writer.WriteState != WriteState.Prolog)
        {
            // Close all open elements, including the top-level <stream>
            await writer.WriteEndDocumentAsync();
            await FlushCommand();
        }
    }

    public sealed override ValueTask DisposeAsync()
    {
        return Close();
    }
}
