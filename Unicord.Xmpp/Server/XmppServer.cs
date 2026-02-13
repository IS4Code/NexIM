using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server;

public class XmppServer : IXmppReceiver
{
    internal SessionsManager Sessions { get; }

    public XmppServer(SessionsManager sessions)
    {
        Sessions = sessions;
    }

    public ValueTask<IStanzaHandler> Connected(IXmppSession session)
    {
        return new(new StreamHandler(this, session));
    }
}
