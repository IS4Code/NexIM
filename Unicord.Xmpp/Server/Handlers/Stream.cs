using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal sealed class Stream : BaseStreamHandler<CommandContext>, IXmppReceivingHandler
{
    const bool supportsIqAuth = true;

    string IXmppHandler.DefaultNamespace => Context.Session.DefaultNamespace;

    async ValueTask IXmppReceivingHandler.StreamStarted()
    {
        var session = Context.Session;

        // Send features
        await using(var features = await session.Features())
        {
            if(!session.IsAuthenticated && session.CanUpgradeTls)
            {
                await using var tls = await features.StartTls();
                if(!session.IsSecure)
                {
                    // Require secure channel before proceeding
                    await tls.Required();
                    return;
                }
            }
            
            if(session.CanCompress)
            {
                await using var comp = await features.Compression();
                await comp.Method(CompressionMethod.ZLib.ToToken());
            }

            if(session.IsAuthenticated)
            {
                if(session.RemoteResource == null)
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
                    if(session.RemoteCertificate != null)
                    {
                        // Certificate could be used
                        await sasl.Mechanism(SaslMechanism.External.ToToken());
                    }
                    if(session.IsSecure)
                    {
                        // Plaintext password requires a secure connection
                        await sasl.Mechanism(SaslMechanism.Plain.ToToken());
                    }
                }

                if(session.IsSecure && supportsIqAuth)
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
            await using(var sessionFeature = await features.Session())
            {
                await sessionFeature.Optional();
            }

            // Normal server features

            await features.RosterVersion();
            await features.PreApproval();
        }
    }

    async ValueTask IXmppReceivingHandler.StreamStopped()
    {
        if(Context.Session.ClientSession is { } session)
        {
            if(Context.Server.GetAccount(this.GetAccountName()) is { } account)
            {
                account.RemoveSession(session);
            }
        }
    }

    protected async override ValueTask OnTlsStart()
    {
        var session = Context.Session;
        if(!session.CanUpgradeTls)
        {
            await session.TlsFailure();
            return;
        }
        await session.TlsProceed();
    }

    protected async override ValueTask<ICompressionHandler> OnCompress()
    {
        return this.GetHandler<Compression>();
    }

    protected async override ValueTask OnSaslAuth(Token<SaslMechanism>? mechanismToken, TemporaryUtf8String? data)
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

        if(await Context.Server.AuthenticatePlain(data, username => ClientSession.GetAccount(new XmppAddress(username, this.GetLocalResource().Address.Host))) is not { } accountName)
        {
            throw XmppSaslException.NotAuthorized();
        }

        var session = Context.Session;

        // Not bound yet
        session.ClientSession = new ClientSession(session)
        {
            Identifier = null,
            AccountName = accountName
        };

        await session.SaslSuccess();
    }

    protected async override ValueTask OnSaslResponse(TemporaryUtf8String? data)
    {
        await Program.NotImplemented<object>();
    }

    protected async override ValueTask OnSaslAbort()
    {
        // TODO Abort
        throw XmppSaslException.Aborted();
    }

    protected override ValueTask<IMessageHandler> OnMessage(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return Context.Server.GetMessageHandler(Context.Session, stanza);
    }

    protected override ValueTask<IPresenceHandler> OnPresence(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return Context.Server.GetPresenceHandler(Context.Session, stanza);
    }

    protected override ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return Context.Server.GetInfoQueryHandler(Context.Session, stanza);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }

    protected override ValueTask<IFeaturesHandler> OnFeatures()
    {
        return Program.NotImplemented<IFeaturesHandler>();
    }

    protected override ValueTask<IStreamErrorHandler> OnError()
    {
        return Program.NotImplemented<IStreamErrorHandler>();
    }

    protected async override ValueTask OnTlsProceed()
    {
        await Program.NotImplemented<object>();
    }

    protected async override ValueTask OnTlsFailure()
    {
        await Program.NotImplemented<object>();
    }

    protected override ValueTask<ICompressionFailureHandler> OnCompressionFailure()
    {
        return Program.NotImplemented<ICompressionFailureHandler>();
    }

    protected async override ValueTask OnCompressed()
    {
        await Program.NotImplemented<object>();
    }

    protected async override ValueTask OnSaslChallenge(TemporaryUtf8String? data)
    {
        await Program.NotImplemented<object>();
    }

    protected override ValueTask<ISaslFailureHandler> OnSaslFailure()
    {
        return Program.NotImplemented<ISaslFailureHandler>();
    }

    protected async override ValueTask OnSaslSuccess()
    {
        await Program.NotImplemented<object>();
    }
}
