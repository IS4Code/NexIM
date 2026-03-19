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
                    // TODO Log suppressed exceptions
                    await HandleException(xe);
                }
                catch(Exception e) when(GetXmppException<XmppSaslException>(e, out var xe))
                {
                    await HandleException(xe);
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

    async ValueTask<bool> HandleException(XmppStanzaException exception)
    {
        AbortCommand();

        if(lastStanza?.Type is StanzaType.Result or StanzaType.Error)
        {
            return false;
        }

        IStreamHandler errorHandler = this;
        var stanza = new Stanza(Type: StanzaType.Error.ToToken(), Identifier: lastStanza?.Identifier ?? default);

        IStanzaHandler command;
        switch(lastStanza?.Kind)
        {
            case StanzaKind.Message:
                command = await errorHandler.Message(stanza);
                break;
            case StanzaKind.Presence:
                command = await errorHandler.Presence(stanza);
                break;
            case StanzaKind.InfoQuery:
                command = await errorHandler.InfoQuery(stanza);
                break;
            default:
                return false;
        }
        try
        {
            await using var err = await command.Error(exception.Type?.ToToken(), exception.Code);
            await exception.Output(err);
            return true;
        }
        finally
        {
            await command.DisposeAsync();
        }
    }

    async ValueTask HandleException(XmppSaslException exception)
    {
        AbortCommand();

        IStreamHandler errorHandler = this;
        await using var error = await errorHandler.SaslFailure();
        await exception.Output(error);
    }

    async ValueTask HandleException(XmppStreamException exception)
    {
        AbortCommand();

        ITransportHandler errorHandler = this;
        await using var error = await errorHandler.Error();
        await exception.Output(error);
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

    void AbortCommand()
    {
        int count = handlers.Count;
        while(handlers.TryPop(out var top))
        {
            if(top is IDisposable disposable)
            {
                // Aborting cleanup
                disposable.Dispose();
            }
        }

        // Ignore the rest of the command
        while(count-- > 0)
        {
            handlers.Push(NullHandler.Instance);
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
                    lastStanza = new(StanzaKind.InfoQuery, stanza.Type?.ToEnum(), stanza.Identifier);
                    return Success(mainHandler.InfoQuery(stanza));
                }
                case 7 when elementName == Message:
                {
                    var stanza = ParseStanza(reader);
                    lastStanza = new(StanzaKind.Message, stanza.Type?.ToEnum(), stanza.Identifier);
                    return Success(mainHandler.Message(stanza));
                }
                case 8 when elementName == Presence:
                {
                    var stanza = ParseStanza(reader);
                    lastStanza = new(StanzaKind.Presence, stanza.Type?.ToEnum(), stanza.Identifier);
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
                                stanza.Identifier = Token<StanzaIdentifier>.FromAtomized(reader.NameTable.Add(reader.Value));
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

    static readonly XmppStanzaException featureNotImplemented = XmppStanzaException.FeatureNotImplemented();
    bool GetXmppException<TException>(Exception e, [MaybeNullWhen(false)] out TException xmppException) where TException : XmppException
    {
        switch(e)
        {
            case TException xe:
                xmppException = xe;
                return true;
            case { InnerException: { } inner } when GetXmppException(inner, out xmppException):
                return true;
            case NotImplementedException when GetXmppException(featureNotImplemented, out xmppException):
                return true;
            default:
                xmppException = null;
                return false;
        }
    }

    readonly record struct StanzaInfo(StanzaKind Kind, StanzaType? Type, Token<StanzaIdentifier>? Identifier);

    sealed class PayloadHandlers : Stack<IPayloadHandler>, IAsyncDisposable
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
