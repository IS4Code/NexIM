using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary.Standard;
using static XmppHandlerSession;

public abstract class XmppXmlListener<TSession> : XmppListener<TSession> where TSession : XmppXmlSession
{
    readonly XmppNameTable nametable;

    protected XmlReaderSettings ReaderSettings { get; }
    protected XmlWriterSettings WriterSettings { get; }

    static readonly XmppDecoder decoder = new();
    protected XmppDecoder Decoder => decoder;

    protected virtual bool PrettyOutput => false;

    public XmppXmlListener(IXmppReceiver<TSession> receiver, ConformanceLevel conformanceLevel) : base(receiver)
    {
        nametable = new();

        ReaderSettings = new()
        {
            Async = true,
            CheckCharacters = false,
            CloseInput = false,
            ConformanceLevel = conformanceLevel,
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
            ConformanceLevel = conformanceLevel,
            Encoding = new UTF8Encoding(false),
            Indent = PrettyOutput,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = true,
            NewLineOnAttributes = PrettyOutput,
            WriteEndDocumentOnClose = false
        };
    }

    protected abstract ValueTask Read(TSession session, CancellationToken cancellationToken);

    protected async ValueTask HandleSession(TSession session, CancellationToken cancellationToken)
    {
        // Receive the session and prepare handler for incoming commands
        await using var handler = await Receiver.Connected(session);

        session.MainHandler = handler;

        try
        {
            // Dispose all handlers at the end
            await using var handlers = session.Handlers;

            while(await session.Reader.ReadAsync())
            {
                try
                {
                    await Read(session, cancellationToken);
                }
                catch(Exception e) when(GetXmppException<XmppStanzaException>(e, out var xe))
                {
                    await HandleException(xe, session, session.LastStanza);
                }
                catch(Exception e) when(GetXmppException<XmppSaslException>(e, out var xe))
                {
                    await HandleException(xe, session);
                }
                finally
                {
                    await session.FlushCommand();
                }
            }
        }
        catch(XmlException e)
        {
            await HandleException(e, session, 1);
        }
        catch(Exception e) when(GetXmppException<XmppStreamException>(e, out var xe))
        {
            await HandleException(xe, session);
            throw;
        }
        catch when(Program.SuppressUnexpectedExceptions())
        {
            await HandleException(XmppStreamException.InternalServerError(), session);
            throw;
        }
        finally
        {
            if(session.StreamIdentifier != null)
            {
                await handler.StreamStopped();
            }
        }
    }

    async ValueTask HandleException(XmppStanzaException exception, TSession session, StanzaInfo? lastStanza)
    {
        IStreamHandler errorHandler = session;

        IStanzaHandler? command = null;
        await OnError(exception, session, async exc => {
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

    async ValueTask HandleException(XmppSaslException exception, TSession session)
    {
        IStreamHandler errorHandler = session;

        ISaslFailureHandler? command = null;
        await OnError(exception, session, async exc => {
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

    ValueTask HandleException(XmppStreamException exception, TSession session)
    {
        ITransportHandler errorHandler = session;

        return OnError(exception, session, _ => errorHandler.Error());
    }

    async ValueTask HandleException(XmlException exception, TSession session, int commandDepth)
    {
        var reader = session.Reader;
        if(reader.Depth <= commandDepth && (reader.EOF || !session.Connected || await session.CheckFinished()))
        {
            // Terminated at the top level
            return;
        }

        await HandleException(XmppStreamException.XmlNotWellFormed(), session);
    }

    async ValueTask OnError<TException, THandler>(TException exception, TSession session, Func<TException, ValueTask<THandler>> errorHandler) where TException : XmppException<THandler> where THandler : IPayloadHandler
    {
        var errors = new List<TException>
        {
            exception
        };
        while(session.Handlers.TryPop(out var top))
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

    protected async ValueTask StreamStarted(XmlReader reader, XmlWriter writer, XmppSession session)
    {
        await writer.WriteAttributeStringAsync(null, Version.Value, null, "1.0");

        session.RemoteLanguage = reader.GetAttribute(Lang.Value, XmlNs.Value);

        // TODO Pick the best language
        session.LocalLanguage = session.DefaultLanguage;

        await writer.WriteAttributeStringAsync(null, Lang.Value, XmlNs.Value, session.LocalLanguage);

        session.StreamIdentifier = Guid.NewGuid().ToString("N");
        await writer.WriteAttributeStringAsync(null, Id.Value, null, session.StreamIdentifier);

        if(reader.GetAttribute("to") is not { } to)
        {
            throw XmppStreamException.HostUnknown("Destination address missing.");
        }

        // TODO Verify that the resource matches exactly the host of the server
        session.LocalResource = XmppResource.Parse(to.AsMemory(), reader.NameTable);

        await writer.WriteAttributeStringAsync(null, From.Value, null, session.LocalResource.ToString());
    }

    protected ValueTask<XmppDecoder.Result> EnterCommand(TSession session, XmlReader reader, IStreamHandler handler)
    {
        var elementName = reader.LocalName;
        if(reader.NamespaceURI == JabberClientNs)
        {
            switch(elementName.Length)
            {
                case 2 when elementName == Iq:
                {
                    var stanza = ParseStanza(reader);
                    session.LastStanza = new(StanzaKind.InfoQuery, stanza.Identifier);
                    return Success(handler.InfoQuery(stanza));
                }
                case 7 when elementName == Message:
                {
                    var stanza = ParseStanza(reader);
                    session.LastStanza = new(StanzaKind.Message, stanza.Identifier);
                    return Success(handler.Message(stanza));
                }
                case 8 when elementName == Presence:
                {
                    var stanza = ParseStanza(reader);
                    session.LastStanza = new(StanzaKind.Presence, stanza.Identifier);
                    return Success(handler.Presence(stanza));
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
        session.LastStanza = null;
        return Decoder.DecodePayload(reader, handler);

        static async ValueTask<XmppDecoder.Result> Success<THandler>(ValueTask<THandler> task) where THandler : IPayloadHandler
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
}
