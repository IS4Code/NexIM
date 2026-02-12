using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol;

public interface IXmppReceiver
{
    ValueTask<IStanzaHandler> Connected(IXmppSession session);
}
