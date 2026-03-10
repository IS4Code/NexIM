using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Grammar;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

using static Vocabulary.Standard;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> 
/// that reads XMPP commands from input and passes them
/// to an <see cref="IXmppReceivingHandler"/> instance.
/// </summary>
public abstract class XmppHandlerSession : XmppXmlSession
{
    protected abstract int TopLevelReaderDepth { get; }

    static readonly ClientDecoder decoder = new();
    public override string DefaultNamespace => ClientDecoder.Namespace;

    IXmppReceivingHandler mainHandler = NullHandler.Instance;
    readonly PayloadHandlers handlers = new();

    StanzaInfo? lastStanza;

    protected abstract ValueTask Read(CancellationToken cancellationToken);

    public async ValueTask Run(IXmppReceivingHandler mainHandler, CancellationToken cancellationToken)
    {
        this.mainHandler = mainHandler;

        try
        {
            // Dispose all handlers at the end
            await using var handlers = this.handlers;

            while(await Reader.ReadAsync())
            {
                try
                {
                    await Read(cancellationToken);
                }
                catch(Exception e) when(GetXmppException<XmppStanzaException>(e, out var xe))
                {
                    await HandleException(xe);
                }
                catch(Exception e) when(GetXmppException<XmppSaslException>(e, out var xe))
                {
                    await HandleException(xe);
                }
                finally
                {
                    await FlushCommand();
                }
            }
        }
        catch(XmlException e)
        {
            await HandleException(e);
        }
        catch(Exception e) when(GetXmppException<XmppStreamException>(e, out var xe))
        {
            await HandleException(xe);
            throw;
        }
        catch when(Program.SuppressUnexpectedExceptions())
        {
            await HandleException(XmppStreamException.InternalServerError());
            throw;
        }
        finally
        {
            if(StreamIdentifier != null)
            {
                await mainHandler.StreamStopped();
            }
        }
    }

    protected async ValueTask StreamsConnected(XmlReader reader, XmlWriter writer)
    {
        await writer.WriteAttributeStringAsync(null, Version.Value, null, "1.0");

        RemoteLanguage = reader.GetAttribute(Lang.Value, XmlNs.Value);

        // TODO Pick the best language
        LocalLanguage = DefaultLanguage;

        await writer.WriteAttributeStringAsync(null, Lang.Value, XmlNs.Value, LocalLanguage);

        StreamIdentifier = Guid.NewGuid().ToString("N");
        await writer.WriteAttributeStringAsync(null, Id.Value, null, StreamIdentifier);

        if(reader.GetAttribute("to") is not { } to)
        {
            throw XmppStreamException.HostUnknown("Destination address missing.");
        }

        // TODO Verify that the resource matches exactly the host of the server
        LocalResource = XmppResource.Parse(to.AsMemory(), reader.NameTable);

        await writer.WriteAttributeStringAsync(null, From.Value, null, LocalResource.ToString());
    }

    protected async ValueTask EnterStream()
    {
        await mainHandler.StreamStarted();
    }

    protected async ValueTask EnterCommand(XmlReader reader)
    {
        bool isEmpty = reader.IsEmptyElement;
        if(await HandleCommand(reader) is (true, var commandHandler))
        {
            // Recognized command type
            await EnterHandler(commandHandler, isEmpty);
        }
        else
        {
            // Unknown type
            await ReadUnknown(mainHandler, reader);
        }
    }

    protected async ValueTask EnterPayload(XmlReader reader)
    {
        bool isEmpty = reader.IsEmptyElement;
        if(await decoder.DecodePayload(reader, handlers.Get<IPayloadHandler>()) is (true, var payloadHandler))
        {
            // Recognized payload type
            await EnterHandler(payloadHandler, isEmpty);
        }
        else
        {
            // Unknown element
            await ReadUnknown(handlers.Get<IPayloadHandler>(), reader);
        }
    }

    private static async ValueTask ReadUnknown(IPayloadHandler handler, XmlReader reader)
    {
        using var subtreeReader = reader.ReadSubtree();
        // The reader is deliberately not positioned at an element because it was already decoded
        await handler.Other(subtreeReader);
        while(await subtreeReader.ReadAsync())
        {
            // Skip all unread nodes
            await subtreeReader.SkipAsync();
        }
    }

    protected async ValueTask ExitPayload()
    {
        if(!handlers.TryPop(out var top))
        {
            return;
        }
        await top.DisposeAsync();
    }

    ValueTask EnterHandler(IPayloadHandler? handler, bool isEmpty)
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

    async ValueTask HandleException(XmppStanzaException exception)
    {
        IStreamHandler errorHandler = this;

        IStanzaHandler? command = null;
        await OnError(exception, async exc => {
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
            return await command.Error(exc.Type?.ToToken(), exc.Code);
        });
        if(command != null)
        {
            await command.DisposeAsync();
        }
    }

    async ValueTask HandleException(XmppSaslException exception)
    {
        IStreamHandler errorHandler = this;

        ISaslFailureHandler? command = null;
        await OnError(exception, async exc => {
            if(command == null)
            {
                command = await errorHandler.SaslFailure();
            }
            return command;
        });
        if(command != null)
        {
            await command.DisposeAsync();
        }
    }

    ValueTask HandleException(XmppStreamException exception)
    {
        ITransportHandler errorHandler = this;

        return OnError(exception, _ => errorHandler.Error());
    }

    async ValueTask HandleException(XmlException exception)
    {
        var reader = Reader;
        if(reader.Depth <= TopLevelReaderDepth && (reader.EOF || !Connected || await CheckFinished()))
        {
            // Terminated at the top level
            return;
        }

        await HandleException(XmppStreamException.XmlNotWellFormed());
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

        foreach(var exc in errors)
        {
            await using var err = await errorHandler(exc);
            await exc.Output(err);
        }
    }

    ValueTask<Decoder.Result> HandleCommand(XmlReader reader)
    {
        var elementName = reader.LocalName;
        if(reader.NamespaceURI == (object)DefaultNamespace)
        {
            switch(elementName.Length)
            {
                case 2 when elementName == InfoQuery:
                {
                    var stanza = ParseStanza(reader);
                    lastStanza = new(StanzaKind.InfoQuery, stanza.Identifier);
                    return Success(mainHandler.InfoQuery(stanza));
                }
                case 7 when elementName == Message:
                {
                    var stanza = ParseStanza(reader);
                    lastStanza = new(StanzaKind.Message, stanza.Identifier);
                    return Success(mainHandler.Message(stanza));
                }
                case 8 when elementName == Presence:
                {
                    var stanza = ParseStanza(reader);
                    lastStanza = new(StanzaKind.Presence, stanza.Identifier);
                    return Success(mainHandler.Presence(stanza));
                }
                case 3:
                case 4:
                case 5:
                case 6:
                    // Contiguous to compile to CIL switch
                    break;
            }
        }

        // Not a stanza - decode normally
        lastStanza = null;
        return decoder.DecodePayload(reader, mainHandler);

        static async ValueTask<Decoder.Result> Success<THandler>(ValueTask<THandler> task) where THandler : IPayloadHandler
        {
            return new(true, await task);
        }
    }

    private Stanza ParseStanza(XmlReader reader)
    {
        var stanza = new Stanza();
        if(reader.MoveToFirstAttribute())
        {
            do
            {
                var attrName = reader.LocalName;
                if(reader.NamespaceURI == Empty)
                {
                    switch(attrName.Length)
                    {
                        case 2:
                            if(attrName == Id)
                            {
                                stanza.Identifier = reader.Value;
                                continue;
                            }
                            else if(attrName == To)
                            {
                                stanza.To = XmppResource.Parse(reader.Value.AsMemory(), reader.NameTable);
                                continue;
                            }
                            break;
                        case 4:
                            if(attrName == Type)
                            {
                                stanza.Type = Token<StanzaType>.FromAtomized(reader.NameTable.Add(reader.Value));
                            }
                            else if(attrName == From)
                            {
                                stanza.From = XmppResource.Parse(reader.Value.AsMemory(), reader.NameTable);
                                continue;
                            }
                            break;
                        case 3:
                            // Contiguous to compile to CIL switch
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

    protected bool GetXmppException<TException>(Exception e, [MaybeNullWhen(false)] out TException xmppException) where TException : XmppException
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

    protected record struct StanzaInfo(StanzaKind Kind, string? Identifier);

    protected enum StanzaKind
    {
        Message,
        Presence,
        InfoQuery
    }

    protected class PayloadHandlers : Stack<IPayloadHandler>, IAsyncDisposable
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
