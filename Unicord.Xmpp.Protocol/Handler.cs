using System.Threading.Tasks;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Protocol;

[ComplexType]
public interface IStreamHandler : IPayloadHandler
{
    [Name("features", "http://etherx.jabber.org/streams")]
    ValueTask<IFeaturesHandler> Features();
}

public interface IStanzaHandler : IStreamHandler
{
    ValueTask<IMessageHandler> Message(in Stanza stanza);
    ValueTask<IPresenceHandler> Presence(in Stanza stanza);
    ValueTask<IInfoQueryHandler> InfoQuery(in Stanza stanza);
}
