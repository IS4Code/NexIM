using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class StreamHandler : CommandHandler, IXmppReceivingHandler
{
    public StreamHandler(XmppServer server, IXmppSession session) : base(server, session, session.StreamIdentifier)
    {

    }

    async ValueTask IXmppReceivingHandler.StreamStarted()
    {
        // Send features
        await using(var features = await Session.Features())
        {
            if(Session.CanUpgradeTls)
            {
                await using var tls = await features.StartTls();
                if(!Session.IsSecure)
                {
                    await tls.Required();
                    // All other features require secure channel
                    return;
                }
            }

            await features.IqAuth();
        }
    }

    async ValueTask IStreamTlsHandler.StartTls()
    {
        if(!Session.CanUpgradeTls)
        {
            await Session.FailureTls();
            return;
        }
        await Session.ProceedTls();
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
        ValidateSender(stanza);
        if(stanza.To is { } to && !to.IsNarrowerThan(Session.LocalResource))
        {
            // Someone else is the receiver
            return Program.NotImplemented<IInfoQueryHandler>();
        }
        return new(new InfoQuery(Server, Session, stanza));
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }

    ValueTask<IFeaturesHandler> IStreamHandler.Features()
    {
        return Program.NotImplemented<IFeaturesHandler>();
    }

    async ValueTask IStreamTlsHandler.ProceedTls()
    {
        await Program.NotImplemented<object>();
    }

    async ValueTask IStreamTlsHandler.FailureTls()
    {
        await Program.NotImplemented<object>();
    }
}
