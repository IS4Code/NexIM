using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary.Standard;

public abstract class XmppStreamListener<TClient> : XmppXmlListener<XmppStreamSession>
{
    protected override bool PrettyOutput => true;

    public XmppStreamListener(IXmppReceiver<XmppStreamSession> receiver) : base(receiver, ConformanceLevel.Document)
    {

    }

    protected abstract ValueTask<XmppStreamSession> StartSession(TClient client, CancellationToken cancellationToken);

    protected async ValueTask HandleStream(TClient client, CancellationToken cancellationToken)
    {
        // Initialize outgoing session
        await using var session = await StartSession(client, cancellationToken);

        // Receive the session and prepare handler for incoming commands
        await using var handler = await Receiver.Connected(session);

        StanzaInfo? lastStanza = null;

        await using PayloadHandlers handlers = new();
        try
        {
            while(await Read(out var reader))
            {
                try
                {
                    int depth = reader.Depth;
                    switch(reader.NodeType)
                    {
                        case XmlNodeType.XmlDeclaration:
                            continue;

                        case XmlNodeType.Element:
                            const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;
                            bool isEmpty = reader.IsEmptyElement;
                            switch(depth)
                            {
                                case 0:
                                    // Root stream element
                                    if(reader.NamespaceURI != StreamsNs)
                                    {
                                        throw XmppStreamException.BadNamespacePrefix();
                                    }
                                    if(reader.LocalName != Stream)
                                    {
                                        throw XmppStreamException.BadFormat();
                                    }

                                    var writer = session.Writer;

                                    await writer.WriteStartElementAsync(Stream.Value, Stream.Value, StreamsNs.Value);
                                    await writer.WriteAttributeStringAsync(Xmlns.Value, Stream.Value, XmlnsNs.Value, StreamsNs.Value);
                                    await writer.WriteAttributeStringAsync(null, Xmlns.Value, null, JabberClientNs.Value);

                                    await StreamStarted(reader, writer, session);

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
                                    if(await Decoder.DecodePayload(reader, handlers.Get<IPayloadHandler>()) is (true, var payloadHandler))
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
                            var stanza = new Stanza(Type: StanzaType.Error.ToToken(), Identifier: lastStanza?.Identifier);
                            command = lastStanza?.Kind switch
                            {
                                StanzaKind.InfoQuery => await errorHandler.InfoQuery(stanza),
                                StanzaKind.Presence => await errorHandler.Presence(stanza),
                                _ => await errorHandler.Message(stanza)
                            };
                        }
                        return await command.Error(exc.Type?.ToToken());
                    });
                    if(command != null)
                    {
                        await command.DisposeAsync();
                    }
                }
                finally
                {
                    await session.Flush();
                }
            }
        }
        catch(XmlException)
        {
            var reader = session.Reader;
            if(reader.Depth <= 1 && (session.Reader.EOF || !await session.CheckMoreData()))
            {
                // Terminated at the top level
                return;
            }

            await OnStreamError(XmppStreamException.XmlNotWellFormed());
        }
        catch(Exception e) when(GetXmppException<XmppStreamException>(e, out var xe))
        {
            await OnStreamError(xe);
            throw;
        }
        catch when(!Debugger.IsAttached)
        {
            await OnStreamError(XmppStreamException.InternalServerError());
            throw;
        }
        finally
        {
            if(session.StreamIdentifier != null)
            {
                await handler.StreamStopped();
            }
        }

        ValueTask<bool> Read(out XmlReader reader)
        {
            reader = session.Reader;
            return new(reader.ReadAsync());
        }

        ValueTask OnStreamError(XmppStreamException exception)
        {
            ITransportHandler errorHandler = session;
            return OnError(exception, _ => errorHandler.Error());
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
            await writer.FlushAsync();
        }
    }
}
