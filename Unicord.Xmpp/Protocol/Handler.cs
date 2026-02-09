using System.Threading.Tasks;
using System.Xml.Linq;

namespace Unicord.Xmpp.Protocol;

public interface IXmppHandler
{
    ValueTask<IFeaturesHandler> Features();
    ValueTask<IMessageHandler> Message(in Stanza stanza);
    ValueTask<IPresenceHandler> Presence(in Stanza stanza);
    ValueTask<IInfoQueryHandler> InfoQuery(in Stanza stanza);
    ValueTask Other(XElement message);
}
