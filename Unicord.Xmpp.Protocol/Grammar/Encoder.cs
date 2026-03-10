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
}

public abstract class ClientEncoder : Encoder
{
    public override string DefaultNamespace => Vocabulary.Standard.JabberClientNs.Value;
}
