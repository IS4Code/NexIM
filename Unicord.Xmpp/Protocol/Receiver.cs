using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol;

public interface IXmppReceiver
{
    ValueTask<IXmppHandler> Connected(IXmppSession session);
}
