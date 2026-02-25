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
            
            if(Session.CanCompress)
            {
                await using var comp = await features.Compression();
                await comp.Method(CompressionMethod.ZLib.ToToken());
            }

            await features.IqAuth();
            await features.RosterVersion();
            await features.PreApproval();
        }
    }

    async ValueTask IXmppReceivingHandler.StreamStopped()
    {
        if(Session.ClientSession is { } session)
        {
            Server.Sessions.RemoveSession(AccountName, session);
        }
    }

    async ValueTask ITransportHandler.StartTls()
    {
        if(!Session.CanUpgradeTls)
        {
            await Session.FailureTls();
            return;
        }
        await Session.ProceedTls();
    }

    async ValueTask<ICompressionHandler> ITransportHandler.Compress()
    {
        return new Compression(Server, Session, null);
    }

    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        ValidateSender(stanza);
        return new(new Message(Server, Session, stanza));
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        ValidateSender(stanza);
        return new(new Presence(Server, Session, stanza));
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        ValidateSender(stanza);
        return new(new InfoQuery(Server, Session, stanza));
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }

    ValueTask<IFeaturesHandler> ITransportHandler.Features()
    {
        return Program.NotImplemented<IFeaturesHandler>();
    }

    ValueTask<IStreamErrorHandler> ITransportHandler.Error()
    {
        return Program.NotImplemented<IStreamErrorHandler>();
    }

    async ValueTask ITransportHandler.ProceedTls()
    {
        await Program.NotImplemented<object>();
    }

    async ValueTask ITransportHandler.FailureTls()
    {
        await Program.NotImplemented<object>();
    }

    ValueTask<ICompressionFailureHandler> ITransportHandler.CompressionFailure()
    {
        return Program.NotImplemented<ICompressionFailureHandler>();
    }

    async ValueTask ITransportHandler.Compressed()
    {
        await Program.NotImplemented<object>();
    }
}
