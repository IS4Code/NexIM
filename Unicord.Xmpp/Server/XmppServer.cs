using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server;

public class XmppServer : Unicord.Server.Server, IXmppReceiver<IXmppSession>
{
    public XmppServer(SessionsManager sessions, AccountsManager accounts) : base(sessions, accounts)
    {

    }

    public ValueTask<IXmppReceivingHandler> Connected(IXmppSession session)
    {
        return new(new StreamHandler(this, session));
    }
}
