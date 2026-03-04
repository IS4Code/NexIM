using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public abstract class XmppListener<TSession>(IXmppReceiver<TSession> receiver) where TSession : XmppXmlSession
{
    protected IXmppReceiver<TSession> Receiver => receiver;

    public abstract Task RunAsync(CancellationToken cancellationToken = default);
}
