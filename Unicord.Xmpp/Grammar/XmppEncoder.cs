using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Server.Tools;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

internal abstract partial class XmppEncoder : IPayloadHandler
{
    protected abstract XmlWriter Writer { get; }
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<XmppEncoder> ForkInner();
    public abstract ValueTask DisposeAsync();

    async ValueTask IPayloadHandler.Other(XElement payload)
    {
        await payload.WriteToAsync(Writer, CancellationToken);
    }

    static readonly TemporaryString.AsynchronousArrayWriter<XmlWriter> xmlTemporaryStringWriter = static async (segment, writer) => {
        await writer.WriteCharsAsync(segment.Array!, segment.Offset, segment.Count);
    };

    private static partial async ValueTask WriteAsync(XmlWriter writer, TemporaryString str)
    {
        await str.WriteToAsync(xmlTemporaryStringWriter, writer);
    }
}
