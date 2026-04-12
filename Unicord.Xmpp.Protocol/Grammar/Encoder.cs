using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xmpp.Protocol.Grammar;

public abstract partial class Encoder : XmlEncoder, IPayloadHandler, IStreamHandler, IValueXmlEncoder<XmppAddress>, IValueXmlEncoder<XmppResource>
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<Encoder> ForkInner();

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        // An unrecognized element might differ in original xml:lang
        // with the parent, however this does not happen directly
        // in a stanza, because stanza language is always replicated.
        await Writer.WriteNodeWithLanguageAsync(payloadReader, false);
    }

    async ValueTask IValueXmlEncoder<XmppAddress>.Encode(XmlWriter writer, XmppAddress value)
    {
        await writer.WriteStringAsync(value.ToString());
    }

    async ValueTask IValueXmlEncoder<XmppResource>.Encode(XmlWriter writer, XmppResource value)
    {
        await writer.WriteStringAsync(value.ToString());
    }

    public virtual ValueTask DisposeAsync()
    {
        // Finalize element
        return new(Writer.WriteEndElementAsync());
    }

    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        ValueTask task;
        try
        {
            task = WriteStanza(StanzaKind.Message.ToToken(), stanza);
        }
        catch(Exception e)
        {
            task = new(Task.FromException(e));
        }
        return Inner();
        async ValueTask<IMessageHandler> Inner()
        {
            await task;
            return await ForkInner();
        }
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        ValueTask task;
        try
        {
            task = WriteStanza(StanzaKind.Presence.ToToken(), stanza);
        }
        catch(Exception e)
        {
            task = new(Task.FromException(e));
        }
        return Inner();
        async ValueTask<IPresenceHandler> Inner()
        {
            await task;
            return await ForkInner();
        }
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        ValueTask task;
        try
        {
            task = WriteStanza(StanzaKind.InfoQuery.ToToken(), stanza);
        }
        catch(Exception e)
        {
            task = new(Task.FromException(e));
        }
        return Inner();
        async ValueTask<IInfoQueryHandler> Inner()
        {
            await task;
            return await ForkInner();
        }
    }

    async ValueTask WriteStanza(Token<StanzaKind> kind, Stanza stanza)
    {
        var writer = Writer;
        await writer.WriteStartElementAsync(null, kind.Value, DefaultNamespace);

        if(stanza.Type is { } type)
        {
            await writer.WriteAttributeStringAsync(null, Vocabulary.Standard.Type.Value, null, type.Value);
        }
        if(stanza.From is { } from)
        {
            await writer.WriteAttributeStringAsync(null, Vocabulary.Standard.From.Value, null, from.ToString());
        }
        if(stanza.To is { } to)
        {
            await writer.WriteAttributeStringAsync(null, Vocabulary.Standard.To.Value, null, to.ToString());
        }
        if(stanza.Identifier is { } identifier)
        {
            await writer.WriteAttributeStringAsync(null, Vocabulary.Standard.Id.Value, null, identifier.Value);
        }
        if(stanza.Language is { } lang && !lang.Equals(new(writer.XmlLang)))
        {
            await writer.WriteAttributeStringAsync("xml", "lang", XNamespace.Xml.NamespaceName, lang.Value);
        }
    }
}

public abstract class ClientEncoder : Encoder
{
    public override string DefaultNamespace => Vocabulary.Standard.JabberClientNs.Value;
}
