using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Server.Primitives.Xml;

namespace Unicord.Xmpp.Protocol.Grammar;

public abstract partial class Encoder : XmlEncoder, IPayloadHandler, IValueXmlEncoder<XmppResource>
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<Encoder> ForkInner();
    public abstract ValueTask DisposeAsync();

    async ValueTask IPayloadHandler.Other(XElement payload)
    {
        using var reader = payload.CreateReader();
        await Writer.WriteNodeAsync(reader, false);
    }

    async ValueTask IValueXmlEncoder<XmppResource>.Encode(XmlWriter writer, XmppResource value)
    {
        await writer.WriteStringAsync(value.ToString());
    }
}
