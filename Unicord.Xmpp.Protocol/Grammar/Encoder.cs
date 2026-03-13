using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;

namespace Unicord.Xmpp.Protocol.Grammar;

public abstract partial class Encoder : XmlEncoder, IPayloadHandler, IStreamHandler, IValueXmlEncoder<XmppResource>
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<Encoder> ForkInner();

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        await Writer.WriteNodeAsync(payloadReader, false);
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
        var copy = stanza;
        return Inner();
        async ValueTask<IMessageHandler> Inner()
        {
            await WriteStanza(StanzaKind.Message.ToToken(), copy);
            return await ForkInner();
        }
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        var copy = stanza;
        return Inner();
        async ValueTask<IPresenceHandler> Inner()
        {
            await WriteStanza(StanzaKind.Presence.ToToken(), copy);
            return await ForkInner();
        }
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        var copy = stanza;
        return Inner();
        async ValueTask<IInfoQueryHandler> Inner()
        {
            await WriteStanza(StanzaKind.InfoQuery.ToToken(), copy);
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
    }
}

public abstract class ClientEncoder : Encoder
{
    public override string DefaultNamespace => Vocabulary.Standard.JabberClientNs.Value;
}
