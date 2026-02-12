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
        Validate(stanza);
        return Program.NotImplemented<IMessageHandler>();
    }

    ValueTask<IPresenceHandler> IStanzaHandler.Presence(in Stanza stanza)
    {
        Validate(stanza);
        return new(new Presence(Server, Session, stanza));
    }

    ValueTask<IInfoQueryHandler> IStanzaHandler.InfoQuery(in Stanza stanza)
    {
        if(stanza.To is { } to && !to.IsNarrowerThan(Session.LocalResource))
        {
            // Someone else is the receiver
            Program.NotImplemented<object>().AsTask().GetAwaiter().GetResult();
        }
        Validate(stanza);
        return new(new InfoQuery(Server, Session, stanza));
    }

    private void Validate(in Stanza stanza)
    {
        if(stanza.From is { } from && !from.IsNarrowerThan(Session.RemoteResource))
        {
            throw new XmppException("Command is comming from an unauthorized sender.", false);
        }
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }
}
