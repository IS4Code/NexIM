using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server;

public class XmppServer : IXmppReceiver
{
    internal SessionsManager Sessions { get; }
    internal AccountsManager Accounts { get; }

    public XmppServer(SessionsManager sessions, AccountsManager accounts)
    {
        Sessions = sessions;
        Accounts = accounts;
    }

    public ValueTask<IStanzaHandler> Connected(IXmppSession session)
    {
        return new(new StreamHandler(this, session));
    }
}
