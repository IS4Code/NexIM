using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;

namespace Unicord.Xmpp.Protocol.Grammar;

public abstract partial class Encoder : XmlEncoder, IPayloadHandler, IValueXmlEncoder<XmppResource>
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<Encoder> ForkInner();
    public abstract ValueTask DisposeAsync();

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        await Writer.WriteNodeAsync(payloadReader, false);
    }

    async ValueTask IValueXmlEncoder<XmppResource>.Encode(XmlWriter writer, XmppResource value)
    {
        await writer.WriteStringAsync(value.ToString());
    }

    protected async ValueTask WriteStanza(Token<StanzaKind> kind, Stanza stanza)
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
            await writer.WriteAttributeStringAsync(null, Vocabulary.Standard.Id.Value, null, identifier);
        }
    }
}

public abstract class ClientEncoder : Encoder
{
    public override string DefaultNamespace => Vocabulary.Standard.JabberClientNs.Value;
}
