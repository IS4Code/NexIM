using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary;

public abstract class XmppListener<TClient>
{
    readonly XmppNameTable nametable;

    protected XmlReaderSettings ReaderSettings { get; }
    protected XmlWriterSettings WriterSettings { get; }

    readonly IXmppReceiver<XmppStreamSession> receiver;

    const bool prettyOutput = true;

    public XmppListener(IXmppReceiver<XmppStreamSession> receiver)
    {
        this.receiver = receiver;

        nametable = new();

        ReaderSettings = new()
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

        WriterSettings = new()
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
            WriteEndDocumentOnClose = false
        };
    }

    public abstract Task RunAsync(CancellationToken cancellationToken = default);

    protected abstract ValueTask<XmppStreamSession> StartSession(TClient client, CancellationToken cancellationToken);

    protected async ValueTask HandleStream(TClient client, CancellationToken cancellationToken)
    {
        const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

        var session = await StartSession(client, cancellationToken);

        await using var handler = await receiver.Connected(session);

        (StanzaType type, string? id)? lastStanza = null;

        await using PayloadHandlers handlers = new();
        while(await Read(out var reader))
        {
            try
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
                                    if(reader.NamespaceURI != StreamsNs)
                                    {
                                        throw XmppStreamException.BadNamespacePrefix();
                                    }
                                    if(reader.Name != Stream)
                                    {
                                        throw XmppStreamException.BadFormat();
                                    }

                                    if(reader.GetAttribute("to") is not { } to)
                                    {
                                        throw XmppStreamException.HostUnknown("Destination address missing.");
                                    }

                                    // TODO Verify that the resource matches exactly the host of the server
                                    session.LocalResource = XmppResource.Parse(to);

                                    if(reader.GetAttribute(XmlLang, XmlNs) is { } lang)
                                    {
                                        session.Language = lang;
                                    }

                                    var writer = session.Writer;

                                    await writer.WriteStartElementAsync(Stream, Stream, StreamsNs);
                                    await writer.WriteAttributeStringAsync(Xmlns, Stream, XmlnsNs, StreamsNs);
                                    await writer.WriteAttributeStringAsync(null, Xmlns, null, JabberClientNs);

                                    session.StreamIdentifier = Guid.NewGuid().ToString("N");

                                    await writer.WriteAttributeStringAsync(null, Version, null, "1.0");
                                    await writer.WriteAttributeStringAsync(null, From, null, session.LocalResource.ToString());
                                    await writer.WriteAttributeStringAsync(null, Id, null, session.StreamIdentifier);
                                    await writer.WriteAttributeStringAsync(null, XmlLang, XmlNs, session.Language);

                                    // Stream is ready
                                    await handler.StreamStarted();
                                    break;

                                case 1:
                                    // Individual command
                                    if(await EnterCommand(reader, handler, out lastStanza) is (true, var commandHandler))
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
                catch(Exception e) when(GetXmppException<XmppStanzaException>(e, out var xe))
                {
                    IStreamHandler errorHandler = session;

                    IStanzaHandler? command = null;
                    await OnError(xe, async exc => {
                        if(command == null)
                        {
                            var stanza = new Stanza(Type: "error", Identifier: lastStanza?.id);
                            command = lastStanza?.type switch
                            {
                                StanzaType.InfoQuery => await errorHandler.InfoQuery(stanza),
                                StanzaType.Presence => await errorHandler.Presence(stanza),
                                _ => await errorHandler.Message(stanza)
                            };
                        }
                        return await command.Error(exc.Type);
                    });
                    if(command != null)
                    {
                        await command.DisposeAsync();
                    }
                }
            }
            catch(Exception e) when(GetXmppException<XmppStreamException>(e, out var xe))
            {
                IStreamTransportHandler errorHandler = session;
                await OnError(xe, _ => errorHandler.Error());
                throw;
            }
            finally
            {
                await session.Flush();
            }
        }

        ValueTask<bool> Read(out XmlReader reader)
        {
            reader = session.Reader;
            return new(reader.ReadAsync());
        }

        async ValueTask OnError<TException, THandler>(TException exception, Func<TException, ValueTask<THandler>> errorHandler) where TException : XmppException<THandler> where THandler : IPayloadHandler
        {
            var errors = new List<TException>
            {
                exception
            };
            while(handlers.TryPop(out var top))
            {
                try
                {
                    await top.DisposeAsync();
                }
                catch(Exception e2) when(GetXmppException<TException>(e2, out var xe2))
                {
                    errors.Add(xe2);
                }
            }

            var writer = session.Writer;
            foreach(var exc in errors)
            {
                await using var err = await errorHandler(exc);
                await exc.Output(err);
            }
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
                            if(attrName == TypeAttr)
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

    private ValueTask<XmppDecoder.Result> EnterCommand(XmlReader reader, IStreamHandler handler, out (StanzaType, string?)? info)
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
                        info = (StanzaType.InfoQuery, stanza.Identifier);
                        return Success(handler.InfoQuery(stanza));
                    }
                    break;
                case 'm':
                    if(elementName == Message)
                    {
                        var stanza = ParseStanza(reader);
                        info = (StanzaType.Message, stanza.Identifier);
                        return Success(handler.Message(stanza));
                    }
                    break;
                case 'p':
                    if(elementName == Presence)
                    {
                        var stanza = ParseStanza(reader);
                        info = (StanzaType.Presence, stanza.Identifier);
                        return Success(handler.Presence(stanza));
                    }
                    break;
            }
        }

        // Not a stanza - decode normally
        info = null;
        return XmppDecoder.DecodePayload(reader, handler);

        static async ValueTask<XmppDecoder.Result> Success<THandler>(ValueTask<THandler> task) where THandler : IPayloadHandler
        {
            return new(true, await task);
        }
    }

    private bool GetXmppException<TException>(Exception e, [MaybeNullWhen(false)] out TException xmppException) where TException : XmppException
    {
        switch(e)
        {
            case TException xe:
                xmppException = xe;
                return true;
            case { InnerException: { } inner } when GetXmppException(inner, out xmppException):
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
