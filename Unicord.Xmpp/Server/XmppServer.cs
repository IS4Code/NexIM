using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server;

public class XmppServer : IXmppReceiver
{
    public ValueTask<IXmppHandler> Connected(IXmppSession session)
    {
        return new(new StreamHandler(this, session));
    }
}
