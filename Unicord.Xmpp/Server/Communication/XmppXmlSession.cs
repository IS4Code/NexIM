using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Grammar;

namespace Unicord.Xmpp.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> capable of
/// sending synchronized XMPP commands as XML data.
/// </summary>
public abstract class XmppXmlSession : XmppSession
{
    readonly SemaphoreSlim semaphore = new(1, 1);
    readonly IStreamHandler commandHandler;

    public abstract XmlReader Reader { get; }
    public abstract XmlWriter Writer { get; }

    public XmppXmlSession()
    {
        commandHandler = new CommandHandler(this);
    }

    protected async sealed override ValueTask<bool> OnEnter()
    {
        await semaphore.WaitAsync(CancellationToken);
        return true;
    }

    protected async override ValueTask OnExit()
    {
        await FlushCommand();
        semaphore.Release();
    }

    protected abstract ValueTask UpgradeTls();
    protected abstract ValueTask EnableCompression();
    protected abstract ValueTask Authenticated();
    protected abstract ValueTask Close();

    public abstract ValueTask FlushCommand();
    public abstract ValueTask<bool> CheckFinished();

    protected sealed override ValueTask<IFeaturesHandler?> OnFeatures()
    {
        return commandHandler.Features()!;
    }

    protected sealed override ValueTask<IStreamErrorHandler?> OnError()
    {
        return commandHandler.Error()!;
    }

    protected sealed override ValueTask<IMessageHandler?> OnMessage(in Stanza stanza)
    {
        return commandHandler.Message(stanza)!;
    }

    protected sealed override ValueTask<IPresenceHandler?> OnPresence(in Stanza stanza)
    {
        return commandHandler.Presence(stanza)!;
    }

    protected sealed override ValueTask<IInfoQueryHandler?> OnInfoQuery(in Stanza stanza)
    {
        return commandHandler.InfoQuery(stanza)!;
    }

    protected async sealed override ValueTask<bool> OnTlsStart()
    {
        await commandHandler.TlsStart();
        return true;
    }

    protected async sealed override ValueTask<bool> OnTlsProceed()
    {
        await commandHandler.TlsProceed();
        await FlushCommand();

        // Swap to TLS while locked
        await UpgradeTls();
        return true;
    }

    protected async sealed override ValueTask<bool> OnTlsFailure()
    {
        try
        {
            await commandHandler.TlsFailure();
            await FlushCommand();
            return true;
        }
        finally
        {
            // No more commands will be sent
            await Close();
        }
    }

    protected sealed override ValueTask<ICompressionHandler?> OnCompress()
    {
        return commandHandler.Compress()!;
    }

    protected sealed override ValueTask<ICompressionFailureHandler?> OnCompressionFailure()
    {
        return commandHandler.CompressionFailure()!;
    }

    protected async sealed override ValueTask<bool> OnCompressed()
    {
        await commandHandler.Compressed();
        await FlushCommand();

        // Enable compression while locked
        await EnableCompression();
        return true;
    }

    protected async sealed override ValueTask<bool> OnSaslAuth(Token<SaslMechanism>? mechanism, TemporaryUtf8String? data)
    {
        await commandHandler.SaslAuth(mechanism, data);
        return true;
    }

    protected async sealed override ValueTask<bool> OnSaslAbort()
    {
        await commandHandler.SaslAbort();
        return true;
    }

    protected async sealed override ValueTask<bool> OnSaslChallenge(TemporaryUtf8String? data)
    {
        await commandHandler.SaslChallenge(data);
        return true;
    }

    protected sealed override ValueTask<ISaslFailureHandler?> OnSaslFailure()
    {
        return commandHandler.SaslFailure()!;
    }

    protected async sealed override ValueTask<bool> OnSaslResponse(TemporaryUtf8String? data)
    {
        await commandHandler.SaslResponse(data);
        return true;
    }

    protected async sealed override ValueTask<bool> OnSaslSuccess()
    {
        await commandHandler.SaslSuccess();
        await FlushCommand();

        // Finish authentication while locked
        await Authenticated();
        return true;
    }

    protected async sealed override ValueTask<bool> OnOther(XmlReader payloadReader)
    {
        await commandHandler.Other(payloadReader);
        return true;
    }

    protected sealed override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        // Not called because everything is handled
        return default;
    }

    class CommandHandler(XmppXmlSession session) : Encoder
    {
        protected override XmlWriter Writer => session.Writer;
        protected override CancellationToken CancellationToken => session.CancellationToken;
        public override string DefaultNamespace => session.DefaultNamespace;

        protected override ValueTask<Encoder> ForkInner()
        {
            return new(new ElementHandler(session));
        }

        sealed class ElementHandler(XmppXmlSession session) : CommandHandler(session)
        {
            int level = 1;

            protected override ValueTask<Encoder> ForkInner()
            {
                // Reuse the current instance to encode nested elements
                if(Interlocked.Increment(ref level) <= 1)
                {
                    throw new ObjectDisposedException(ToString());
                }
                return new(this);
            }

            public override ValueTask DisposeAsync()
            {
                if(Interlocked.Decrement(ref level) < 0)
                {
                    return default;
                }
                return base.DisposeAsync();
            }
        }
    }
}
