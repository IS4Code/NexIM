using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class StreamHandler : CommandHandler, IXmppHandler
{
    public StreamHandler(XmppServer server, IXmppSession session) : base(server, session, session.StreamIdentifier)
    {

    }

    ValueTask<IFeaturesHandler> IXmppHandler.Features()
    {
        return Program.NotImplemented<IFeaturesHandler>();
    }

    ValueTask<IMessageHandler> IXmppHandler.Message(in Stanza stanza)
    {
        return Program.NotImplemented<IMessageHandler>();
    }

    ValueTask<IPresenceHandler> IXmppHandler.Presence(in Stanza stanza)
    {
        return Program.NotImplemented<IPresenceHandler>();
    }

    ValueTask<IInfoQueryHandler> IXmppHandler.InfoQuery(in Stanza stanza)
    {
        if (stanza.To is { } to && to != Session.LocalResource)
        {
            // Someone else is the receiver
            return Program.NotImplemented<IInfoQueryHandler>();
        }
        return new(new InfoQuery(Server, Session, stanza));
    }

    async ValueTask IXmppHandler.Other(XElement message)
    {
        await Program.NotImplemented<object>();
    }
}
