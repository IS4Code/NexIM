using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary.Standard;

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
        session.LocalResource = XmppResource.Parse(to);

        await writer.WriteAttributeStringAsync(null, From.Value, null, session.LocalResource.ToString());
    }

    protected ValueTask<XmppDecoder.Result> EnterCommand(XmlReader reader, IStreamHandler handler, out StanzaInfo? info)
    {
        var elementName = reader.LocalName;
        var elementNs = reader.NamespaceURI;
        if(elementNs == JabberClientNs)
        {
            switch(elementName[0])
            {
                case 'i':
                    if(elementName == Iq)
                    {
                        var stanza = ParseStanza(reader);
                        info = new(StanzaKind.InfoQuery, stanza.Identifier);
                        return Success(handler.InfoQuery(stanza));
                    }
                    break;
                case 'm':
                    if(elementName == Message)
                    {
                        var stanza = ParseStanza(reader);
                        info = new(StanzaKind.Message, stanza.Identifier);
                        return Success(handler.Message(stanza));
                    }
                    break;
                case 'p':
                    if(elementName == Presence)
                    {
                        var stanza = ParseStanza(reader);
                        info = new(StanzaKind.Presence, stanza.Identifier);
                        return Success(handler.Presence(stanza));
                    }
                    break;
            }
        }

        // Not a stanza - decode normally
        info = null;
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
                    switch(attrName[0])
                    {
                        case 't':
                            if(attrName == Type)
                            {
                                stanza.Type = Token<StanzaType>.FromAtomized(reader.NameTable.Add(reader.Value));
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

    protected record struct StanzaInfo(StanzaKind Kind, string? Identifier);

    protected enum StanzaKind
    {
        Message,
        Presence,
        InfoQuery
    }
}
