using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal sealed class Stream : BaseStreamHandler<ICommandContext>, IXmppReceivingHandler
{
    const bool supportsIqAuth = true;

    string IXmppHandler.DefaultNamespace => this.GetSession().DefaultNamespace;

    async ValueTask IXmppReceivingHandler.StreamStarted()
    {
        var session = this.GetSession();

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
        if(this.TryGetClientSession() is { } session)
        {
            this.GetAccount().RemoveSession(session);
        }
    }

    protected async override ValueTask OnTlsStart()
    {
        var session = this.GetSession();
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

        var session = this.GetSession();

        if(await this.GetServer().AuthenticatePlain(data, username => new XmppAddress(username, this.GetLocalResource().Address.Host).ToAccountName()) is not { } account)
        {
            throw XmppSaslException.NotAuthorized();
        }

        // Not bound yet
        session.ClientSession = new XmppClientSession(account, null, session);

        await session.SaslSuccess();
    }

    static async ValueTask<TResult> NotImplemented<TResult>()
    {
        Debugger.Break();
        throw new NotImplementedException(null, XmppStanzaException.FeatureNotImplemented(ErrorType.Cancel));
    }

    protected async override ValueTask OnSaslResponse(TemporaryUtf8String? data)
    {
        await NotImplemented<object>();
    }

    protected async override ValueTask OnSaslAbort()
    {
        // TODO Abort
        throw XmppSaslException.Aborted();
    }

    protected override ValueTask<IMessageHandler> OnMessage(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return this.GetServer().GetMessageHandler(this.GetSession(), stanza);
    }

    protected override ValueTask<IPresenceHandler> OnPresence(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return this.GetServer().GetPresenceHandler(this.GetSession(), stanza);
    }

    protected override ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return this.GetServer().GetInfoQueryHandler(this.GetSession(), stanza);
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
        return NotImplemented<IFeaturesHandler>();
    }

    protected override ValueTask<IStreamErrorHandler> OnError()
    {
        return NotImplemented<IStreamErrorHandler>();
    }

    protected async override ValueTask OnTlsProceed()
    {
        await NotImplemented<object>();
    }

    protected async override ValueTask OnTlsFailure()
    {
        await NotImplemented<object>();
    }

    protected override ValueTask<ICompressionFailureHandler> OnCompressionFailure()
    {
        return NotImplemented<ICompressionFailureHandler>();
    }

    protected async override ValueTask OnCompressed()
    {
        await NotImplemented<object>();
    }

    protected async override ValueTask OnSaslChallenge(TemporaryUtf8String? data)
    {
        await NotImplemented<object>();
    }

    protected override ValueTask<ISaslFailureHandler> OnSaslFailure()
    {
        return NotImplemented<ISaslFailureHandler>();
    }

    protected async override ValueTask OnSaslSuccess()
    {
        await NotImplemented<object>();
    }
}
