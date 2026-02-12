using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary;

public class XmppListener
{
    readonly XmppVocabulary nametable;
    readonly XmlReaderSettings readerSettings;
    readonly XmlWriterSettings writerSettings;

    readonly TcpListener listener;
    readonly IXmppReceiver receiver;

    const bool prettyOutput = true;

    public XmppListener(IXmppReceiver receiver)
    {
        this.receiver = receiver;

        nametable = new();

        readerSettings = new()
        {
            Async = true,
            CheckCharacters = false,
            CloseInput = false,
            ConformanceLevel = ConformanceLevel.Document,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            NameTable = nametable,
            ValidationType = ValidationType.None,
            XmlResolver = XmlResolver.ThrowingResolver
        };

        writerSettings = new()
        {
            Async = true,
            CheckCharacters = false,
            CloseOutput = false,
            ConformanceLevel = ConformanceLevel.Document,
            Encoding = new UTF8Encoding(false),
            Indent = prettyOutput,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = true,
            NewLineOnAttributes = prettyOutput,
            WriteEndDocumentOnClose = true
        };

        listener = new(IPAddress.Any, 5222);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        listener.Start();
        while(await listener.AcceptTcpClientAsync(cancellationToken) is { } client)
        {
            HandleClient(client, cancellationToken);
        }
    }

    private async void HandleClient(TcpClient client, CancellationToken cancellationToken)
    {
        const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

        try
        {
            await using var stream = client.GetStream();

            using var reader = XmlReader.Create(stream, readerSettings);
            //using var writer = XmlWriter.Create(stream, writerSettings);

            using var writer = XmlWriter.Create(new DuplicatingStream(stream, Console.OpenStandardOutput()), writerSettings);

            var session = new XmppTcpXmlSession(client, writer, cancellationToken);

            var handler = await receiver.Connected(session);

            await using PayloadHandlers handlers = new();
            while(await reader.ReadAsync())
            {
                try
                {
                    int depth = reader.Depth;
                    switch(reader.NodeType)
                    {
                        case XmlNodeType.XmlDeclaration:
                            continue;

                        case XmlNodeType.Element:
                            bool isEmpty = reader.IsEmptyElement;
                            switch(depth)
                            {
                                case 0:
                                    // Root stream element
                                    if(reader.Name != Stream && reader.NamespaceURI != StreamsNs)
                                    {
                                        throw new XmppException("Unexpected root element name.", fatal: true);
                                    }

                                    if(reader.GetAttribute("to") is not { } to)
                                    {
                                        throw new XmppException("Destination address missing.", fatal: true);
                                    }

                                    session.LocalResource = XmppResource.Parse(to);

                                    await writer.WriteStartElementAsync(Stream, Stream, StreamsNs);
                                    await writer.WriteAttributeStringAsync(Xmlns, Stream, XmlnsNs, StreamsNs);
                                    await writer.WriteAttributeStringAsync(null, Xmlns, null, JabberClientNs);

                                    session.StreamIdentifier = Guid.NewGuid().ToString("N");

                                    await writer.WriteAttributeStringAsync(null, Version, null, "1.0");
                                    await writer.WriteAttributeStringAsync(null, From, null, session.LocalResource.ToString());
                                    await writer.WriteAttributeStringAsync(null, Id, null, session.StreamIdentifier);

                                    // Features
                                    await WriteFeatures(session);
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
                                    if(await XmppDecoder.DecodePayload(reader, handlers.Get<IPayloadHandler>()) is (true, var payloadHandler))
                                    {
                                        // Recognized payload type
                                        await EnterHandler(payloadHandler);
                                    }
                                    else
                                    {
                                        // Unknown element
                                        using var subtreeReader = reader.ReadSubtree();
                                        var element = await XElement.LoadAsync(subtreeReader, elementLoadOptions, cancellationToken);
                                        await handlers.Get<IPayloadHandler>().Other(element);
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
                                        handlers.Push(handler);
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
                                    continue;
                                default:
                                    if(!handlers.TryPop(out var top))
                                    {
                                        continue;
                                    }
                                    await top.DisposeAsync();
                                    break;
                            }
                            break;
                    }
                }
                catch(Exception e) when(GetXmppException(e, out var xe))
                {
                    if(xe.Fatal)
                    {
                        // Close connection
                        throw;
                    }

                    Console.WriteLine(e);
                }
                finally
                {
                    await writer.FlushAsync();
                }
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            client.Dispose();
        }
    }

    private Stanza ParseStanza(XmlReader reader)
    {
        var stanza = new Stanza();
        if(reader.MoveToFirstAttribute())
        {
            do
            {
                var attrName = reader.Name;
                if(reader.NamespaceURI == Empty)
                {
                    switch(attrName[0])
                    {
                        case 't':
                            if(attrName == Type)
                            {
                                stanza.Type = reader.Value;
                            }
                            else if(attrName == To)
                            {
                                stanza.To = XmppResource.Parse(reader.Value);
                                continue;
                            }
                            break;
                        case 'f':
                            if(attrName == From)
                            {
                                stanza.From = XmppResource.Parse(reader.Value);
                                continue;
                            }
                            break;
                        case 'i':
                            if(attrName == Id)
                            {
                                stanza.Identifier = reader.Value;
                                continue;
                            }
                            break;
                    }
                }

                // Unknown attribute
                continue;
            }
            while(reader.MoveToNextAttribute());
        }
        return stanza;
    }

    private ValueTask<XmppDecoder.Result> EnterCommand(XmlReader reader, IStanzaHandler handler)
    {
        var elementName = reader.Name;
        var elementNs = reader.NamespaceURI;
        if(elementNs == JabberClientNs)
        {
            switch(elementName[0])
            {
                case 'i':
                    if(elementName == Iq)
                    {
                        var stanza = ParseStanza(reader);
                        return Success(handler.InfoQuery(stanza));
                    }
                    break;
                case 'm':
                    if(elementName == Message)
                    {
                        var stanza = ParseStanza(reader);
                        return Success(handler.Message(stanza));
                    }
                    break;
                case 'p':
                    if(elementName == Presence)
                    {
                        var stanza = ParseStanza(reader);
                        return Success(handler.Presence(stanza));
                    }
                    break;
            }
        }

        // Not a stanza - decode normally
        return XmppDecoder.DecodePayload(reader, handler);

        static async ValueTask<XmppDecoder.Result> Success<THandler>(ValueTask<THandler> task) where THandler : IPayloadHandler
        {
            return new(true, await task);
        }
    }

    private async ValueTask WriteFeatures(IStanzaHandler session)
    {
        await using var features = await session.Features();

        await features.IqAuth();
    }

    private bool GetXmppException(Exception e, [MaybeNullWhen(false)] out XmppException xmppException)
    {
        switch(e)
        {
            case XmppException xe:
                xmppException = xe;
                return true;
            default:
                xmppException = null;
                return false;
        }
    }

    private class PayloadHandlers : Stack<IPayloadHandler>, IAsyncDisposable
    {
        public THandler Get<THandler>() where THandler : IPayloadHandler
        {
            if(!this.TryPeek(out var top) || top is not THandler handler)
            {
                throw new NotSupportedException("The current payload handler does not support this element.");
            }
            return handler;
        }

        public async ValueTask DisposeAsync()
        {
            while(this.TryPop(out var top))
            {
                await top.DisposeAsync();
            }
        }
    }
}
