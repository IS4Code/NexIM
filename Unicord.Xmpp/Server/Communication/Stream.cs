using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class Stream : BaseStreamHandler, IXmppReceivingHandler, ICommandHandler
{
    const bool supportsIqAuth = true;

    public required CommandState State { get; init; }

    async ValueTask IXmppReceivingHandler.StreamStarted()
    {
        var session = State.Session;

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
        if(State.Session.ClientSession is { } session)
        {
            State.Server.Sessions.RemoveSession(this.GetAccountName(), session);
        }
    }

    protected async override ValueTask<bool> OnTlsStart()
    {
        var session = State.Session;
        if(!session.CanUpgradeTls)
        {
            await session.TlsFailure();
            return true;
        }
        await session.TlsProceed();
        return true;
    }

    protected async override ValueTask<ICompressionHandler?> OnCompress()
    {
        return this.GetHandler<Compression>();
    }

    protected async override ValueTask<bool> OnSaslAuth(Token<SaslMechanism>? mechanismToken, TemporaryUtf8String? data)
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

        if(await State.Server.AuthenticatePlain(data, username => ClientSession.GetAccount(new XmppAddress(username, this.GetLocalResource().Address.Host))) is not { } accountName)
        {
            throw XmppSaslException.NotAuthorized();
        }

        var session = State.Session;

        // Not bound yet
        session.ClientSession = new ClientSession(session)
        {
            Identifier = null,
            AccountName = accountName
        };

        await session.SaslSuccess();
        return true;
    }

    protected override ValueTask<bool> OnSaslResponse(TemporaryUtf8String? data)
    {
        return Program.NotImplemented<bool>();
    }

    protected async override ValueTask<bool> OnSaslAbort()
    {
        // TODO Abort
        throw XmppSaslException.Aborted();
    }

    protected override ValueTask<IMessageHandler?> OnMessage(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return State.Server.GetMessageHandler(State.Session, stanza)!;
    }

    protected override ValueTask<IPresenceHandler?> OnPresence(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return State.Server.GetPresenceHandler(State.Session, stanza)!;
    }

    protected override ValueTask<IInfoQueryHandler?> OnInfoQuery(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return State.Server.GetInfoQueryHandler(State.Session, stanza)!;
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }

    protected override ValueTask<IFeaturesHandler?> OnFeatures()
    {
        return Program.NotImplemented<IFeaturesHandler?>();
    }

    protected override ValueTask<IStreamErrorHandler?> OnError()
    {
        return Program.NotImplemented<IStreamErrorHandler?>();
    }

    protected override ValueTask<bool> OnTlsProceed()
    {
        return Program.NotImplemented<bool>();
    }

    protected override ValueTask<bool> OnTlsFailure()
    {
        return Program.NotImplemented<bool>();
    }

    protected override ValueTask<ICompressionFailureHandler?> OnCompressionFailure()
    {
        return Program.NotImplemented<ICompressionFailureHandler?>();
    }

    protected override ValueTask<bool> OnCompressed()
    {
        return Program.NotImplemented<bool>();
    }

    protected override ValueTask<bool> OnSaslChallenge(TemporaryUtf8String? data)
    {
        return Program.NotImplemented<bool>();
    }

    protected override ValueTask<ISaslFailureHandler?> OnSaslFailure()
    {
        return Program.NotImplemented<ISaslFailureHandler?>();
    }

    protected override ValueTask<bool> OnSaslSuccess()
    {
        return Program.NotImplemented<bool>();
    }
}
