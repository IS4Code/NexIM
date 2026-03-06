using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class StreamHandler : CommandHandler, IXmppReceivingHandler
{
    const bool supportsIqAuth = true;

    public StreamHandler(XmppServer server, IXmppSession session) : base(server, session, session.StreamIdentifier)
    {

    }

    async ValueTask IXmppReceivingHandler.StreamStarted()
    {
        // Send features
        await using(var features = await Session.Features())
        {
            if(!Session.IsAuthenticated && Session.CanUpgradeTls)
            {
                await using var tls = await features.StartTls();
                if(!Session.IsSecure)
                {
                    // Require secure channel before proceeding
                    await tls.Required();
                    return;
                }
            }
            
            if(Session.CanCompress)
            {
                await using var comp = await features.Compression();
                await comp.Method(CompressionMethod.ZLib.ToToken());
            }

            if(Session.IsAuthenticated)
            {
                if(Session.RemoteResource == null)
                {
                    // Binding is required
                    await features.Bind();
                }
            }
            else
            {
                // Present authentication options

                await using(var sasl = await features.SaslMechanisms())
                {
                    if(Session.RemoteCertificate != null)
                    {
                        // Certificate could be used
                        await sasl.Mechanism(SaslMechanism.External.ToToken());
                    }
                    if(Session.IsSecure)
                    {
                        // Plaintext password requires a secure connection
                        await sasl.Mechanism(SaslMechanism.Plain.ToToken());
                    }
                }

                if(Session.IsSecure && supportsIqAuth)
                {
                    // Only plaintext is supported for this method
                    await features.IqAuth();
                }
                else
                {
                    // If SASL is the only option, other features can wait for the stream restart
                    return;
                }
            }

            // Support session as no-op
            await using(var session = await features.Session())
            {
                await session.Optional();
            }

            // Normal server features

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

    async ValueTask ITransportHandler.TlsStart()
    {
        if(!Session.CanUpgradeTls)
        {
            await Session.TlsFailure();
            return;
        }
        await Session.TlsProceed();
    }

    async ValueTask<ICompressionHandler> ITransportHandler.Compress()
    {
        return new Compression(Server, Session, null);
    }

    async ValueTask ITransportHandler.SaslAuth(Token<SaslMechanism>? mechanismToken, TemporaryUtf8String? data)
    {
        if(mechanismToken?.ToEnum() is not { } mechanism || mechanism is not (SaslMechanism.Plain or SaslMechanism.Anonymous or SaslMechanism.External))
        {
            throw XmppSaslException.InvalidMechanism();
        }

        if(mechanism != SaslMechanism.Plain)
        {
            // TODO Support other mechanisms
            throw XmppSaslException.NotAuthorized();
        }

        if(await Server.AuthenticatePlain(data, username => ClientSession.GetAccount(new XmppAddress(username, LocalResource.Address.Host))) is not { } accountName)
        {
            throw XmppSaslException.NotAuthorized();
        }

        // Not bound yet
        Session.ClientSession = new ClientSession(Session)
        {
            Identifier = null,
            AccountName = accountName
        };

        await Session.SaslSuccess();
    }

    async ValueTask ITransportHandler.SaslResponse(TemporaryUtf8String? data)
    {
        await Program.NotImplemented<object>();
    }

    async ValueTask ITransportHandler.SaslAbort()
    {
        // TODO Abort
        throw XmppSaslException.Aborted();
    }

    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        ValidateSender(stanza);
        return Server.GetMessageHandler(Session, stanza);
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        ValidateSender(stanza);
        return Server.GetPresenceHandler(Session, stanza);
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        ValidateSender(stanza);
        return Server.GetInfoQueryHandler(Session, stanza);
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

    async ValueTask ITransportHandler.TlsProceed()
    {
        await Program.NotImplemented<object>();
    }

    async ValueTask ITransportHandler.TlsFailure()
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

    async ValueTask ITransportHandler.SaslChallenge(TemporaryUtf8String? data)
    {
        await Program.NotImplemented<object>();
    }

    ValueTask<ISaslFailureHandler> ITransportHandler.SaslFailure()
    {
        return Program.NotImplemented<ISaslFailureHandler>();
    }

    async ValueTask ITransportHandler.SaslSuccess()
    {
        await Program.NotImplemented<object>();
    }
}
