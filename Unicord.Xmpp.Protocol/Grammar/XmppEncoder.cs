using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

public abstract partial class XmppEncoder : XmlEncoder, IPayloadHandler
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<XmppEncoder> ForkInner();
    public abstract ValueTask DisposeAsync();

    async ValueTask IPayloadHandler.Other(XElement payload)
    {
        using var reader = payload.CreateReader();
        await Writer.WriteNodeAsync(reader, false);
    }
}
