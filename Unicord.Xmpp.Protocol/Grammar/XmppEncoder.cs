using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Server.Primitives;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

public abstract partial class XmppEncoder : IPayloadHandler
{
    protected abstract XmlWriter Writer { get; }
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<XmppEncoder> ForkInner();
    public abstract ValueTask DisposeAsync();

    async ValueTask IPayloadHandler.Other(XElement payload)
    {
        using var reader = payload.CreateReader();
        await Writer.WriteNodeAsync(reader, false);
    }

    private static partial async ValueTask WriteAsync(XmlWriter writer, TemporaryString str)
    {
        var value = str.Value;
        await writer.WriteCharsAsync(value.Array!, value.Offset, value.Count);
    }
}
