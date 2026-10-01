using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Server;
using NexIM.Server.Authentication;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Communication;

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
                    // These do not transmit the password
                    await sasl.Mechanism(SaslMechanism.ScramSha256.ToToken());
                    await sasl.Mechanism(SaslMechanism.ScramSha1.ToToken());
                    if(session.IsSecure)
                    {
                        // Plaintext password requires a secure connection
                        await sasl.Mechanism(SaslMechanism.Plain.ToToken());
                    }
                }

                if(session.IsSecure)
                {
                    // Can register
                    await features.IqRegister();
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
            await session.Account.RemoveSession(session);
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
        if(mechanismToken is not { } mechanism)
        {
            throw XmppSaslException.InvalidMechanism();
        }

        var session = this.GetSession();
        var localHost = this.GetLocalResource().Address.Host;

        // Close previous exchange
        await SaslStop(session);

        AccountName ResolveUsername(string username)
        {
            return new XmppAddress(username, localHost).ToAccountName();
        }

        if(this.GetServer().CreateSaslSession(mechanism.Value, session.IsSecure, ResolveUsername) is not { } saslSession)
        {
            throw XmppSaslException.InvalidMechanism();
        }

        session.SaslSession = saslSession;

        await SaslResponse(session, await saslSession.Response(data));
    }

    static async ValueTask<TResult> NotImplemented<TResult>()
    {
        Debugger.Break();
        throw new NotImplementedException(null, XmppStanzaException.FeatureNotImplemented(ErrorType.Cancel));
    }

    protected async override ValueTask OnSaslResponse(TemporaryUtf8String? data)
    {
        var session = this.GetSession();

        if(session.SaslSession is not { } saslSession)
        {
            // No authentication is in progress
            throw XmppSaslException.NotAuthorized();
        }

        await SaslResponse(session, await saslSession.Response(data));
    }

    protected async override ValueTask OnSaslAbort()
    {
        var session = this.GetSession();

        if(session.SaslSession is { } saslSession)
        {
            session.SaslSession = null;
            await saslSession.DisposeAsync();
        }

        throw XmppSaslException.Aborted();
    }

    static async ValueTask SaslResponse(IXmppSession session, SaslResponse response)
    {
        try
        {
            switch(response.Status)
            {
                case SaslStatus.Challenge:
                    await session.SaslChallenge(response.ChallengeData);
                    return;

                case SaslStatus.Success:
                    // Authenticated but not bound yet
                    session.ClientSession = new XmppClientSession(response.Account!, null, session);
                    await session.SaslSuccess(response.ChallengeData);
                    return;

                default:
                    // Failure
                    throw response.Status.ToXmppException()!;
            }
        }
        finally
        {
            if(response.Status != SaslStatus.Challenge)
            {
                // Final status
                await SaslStop(session);
            }
        }
    }

    static async ValueTask SaslStop(IXmppSession session)
    {
        if(session.SaslSession is { } saslSession)
        {
            try
            {
                await saslSession.DisposeAsync();
            }
            finally
            {
                session.SaslSession = null;
            }
        }
    }

    protected override ValueTask<IMessageHandler> OnMessage(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return this.GetServerReceiver().GetMessageHandler(this.GetSession(), stanza);
    }

    protected override ValueTask<IPresenceHandler> OnPresence(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return this.GetServerReceiver().GetPresenceHandler(this.GetSession(), stanza);
    }

    protected override ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza)
    {
        this.ValidateSender(stanza);
        return this.GetServerReceiver().GetInfoQueryHandler(this.GetSession(), stanza);
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

    protected async override ValueTask OnSaslSuccess(TemporaryUtf8String? data)
    {
        await NotImplemented<object>();
    }
}
