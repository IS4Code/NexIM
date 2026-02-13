using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class StreamHandler : CommandHandler, IStanzaHandler
{
    public StreamHandler(XmppServer server, IXmppSession session) : base(server, session, session.StreamIdentifier)
    {

    }

    ValueTask<IFeaturesHandler> IStreamHandler.Features()
    {
        return Program.NotImplemented<IFeaturesHandler>();
    }

    ValueTask<IMessageHandler> IStanzaHandler.Message(in Stanza stanza)
    {
        ValidateSender(stanza);
        return new(new Message(Server, Session, stanza));
    }

    ValueTask<IPresenceHandler> IStanzaHandler.Presence(in Stanza stanza)
    {
        ValidateSender(stanza);
        return new(new Presence(Server, Session, stanza));
    }

    ValueTask<IInfoQueryHandler> IStanzaHandler.InfoQuery(in Stanza stanza)
    {
        if(stanza.To is { } to && !to.IsNarrowerThan(Session.LocalResource))
        {
            // Someone else is the receiver
            Program.NotImplemented<object>().AsTask().GetAwaiter().GetResult();
        }
        ValidateSender(stanza);
        return new(new InfoQuery(Server, Session, stanza));
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }
}
