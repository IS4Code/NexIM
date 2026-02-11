using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

using static XmppVocabulary;

internal abstract partial class XmppEncoder : IPayloadHandler
{
    protected abstract XmlWriter Writer { get; }
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<XmppEncoder> ForkInner();

    async ValueTask IPayloadHandler.Other(XElement payload)
    {
        await payload.WriteToAsync(Writer, CancellationToken);
    }

    public abstract ValueTask DisposeAsync();
}
