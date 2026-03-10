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
    readonly CommandHandler commandHandler;

    public abstract XmlReader Reader { get; }
    public abstract XmlWriter Writer { get; }

    public XmppXmlSession()
    {
        commandHandler = new(this);
    }

    protected abstract ValueTask UpgradeTls();
    protected abstract ValueTask EnableCompression();
    protected abstract ValueTask Authenticated();
    protected abstract ValueTask Close();

    public abstract ValueTask FlushCommand();
    public abstract ValueTask<bool> CheckFinished();

    protected async sealed override ValueTask<IFeaturesHandler?> OnFeatures()
    {
        var handler = new FeaturesHandler(this);
        await handler.Acquire();
        return handler;
    }

    protected async sealed override ValueTask<IStreamErrorHandler?> OnError()
    {
        var handler = new ErrorHandler(this);
        await handler.Acquire();
        return handler;
    }

    protected sealed override ValueTask<IMessageHandler?> OnMessage(in Stanza stanza)
    {
        var handler = new StanzaHandler(StanzaKind.Message, stanza, this);
        return Enter<IMessageHandler>(handler);
    }

    protected sealed override ValueTask<IPresenceHandler?> OnPresence(in Stanza stanza)
    {
        var handler = new StanzaHandler(StanzaKind.Presence, stanza, this);
        return Enter<IPresenceHandler>(handler);
    }

    protected sealed override ValueTask<IInfoQueryHandler?> OnInfoQuery(in Stanza stanza)
    {
        var handler = new StanzaHandler(StanzaKind.InfoQuery, stanza, this);
        return Enter<IInfoQueryHandler>(handler);
    }

    protected async sealed override ValueTask<bool> OnTlsStart()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.TlsStart();
            await FlushCommand();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<bool> OnTlsProceed()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.TlsProceed();
            await FlushCommand();

            // Swap to TLS while locked
            await UpgradeTls();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<bool> OnTlsFailure()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            try
            {
                ITransportHandler handler = commandHandler;
                await handler.TlsFailure();
                await FlushCommand();
                return true;
            }
            finally
            {
                // No more commands will be sent
                await Close();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<ICompressionHandler?> OnCompress()
    {
        var handler = new CompressionHandler(this);
        await handler.Acquire();
        return handler;
    }

    protected async sealed override ValueTask<ICompressionFailureHandler?> OnCompressionFailure()
    {
        var handler = new CompressionFailureHandler(this);
        await handler.Acquire();
        return handler;
    }

    protected async sealed override ValueTask<bool> OnCompressed()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.Compressed();
            await FlushCommand();

            // Enable compression while locked
            await EnableCompression();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<bool> OnSaslAuth(Token<SaslMechanism>? mechanism, TemporaryUtf8String? data)
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.SaslAuth(mechanism, data);
            await FlushCommand();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<bool> OnSaslAbort()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.SaslAbort();
            await FlushCommand();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<bool> OnSaslChallenge(TemporaryUtf8String? data)
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.SaslChallenge(data);
            await FlushCommand();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<ISaslFailureHandler?> OnSaslFailure()
    {
        var handler = new SaslFailureHandler(this);
        await handler.Acquire();
        return handler;
    }

    protected async sealed override ValueTask<bool> OnSaslResponse(TemporaryUtf8String? data)
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.SaslResponse(data);
            await FlushCommand();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected async sealed override ValueTask<bool> OnSaslSuccess()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            ITransportHandler handler = commandHandler;
            await handler.SaslSuccess();
            await FlushCommand();

            // Finish authentication while locked
            await Authenticated();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async ValueTask<THandler?> Enter<THandler>(StanzaHandler handler) where THandler : IPayloadHandler
    {
        await handler.Acquire();
        return (THandler)(IPayloadHandler)handler;
    }

    protected async sealed override ValueTask<bool> OnOther(XmlReader payloadReader)
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            await Writer.WriteNodeAsync(payloadReader, false);
            await FlushCommand();
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected sealed override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        // Not called because everything is handled
        return default;
    }

    abstract class PayloadHandler : Encoder
    {
        protected XmppXmlSession Session { get; }

        protected override XmlWriter Writer => Session.Writer;
        protected override CancellationToken CancellationToken => Session.CancellationToken;
        public override string DefaultNamespace => Session.DefaultNamespace;

        public PayloadHandler(XmppXmlSession session)
        {
            Session = session;
        }

        protected override ValueTask<Encoder> ForkInner()
        {
            return new(new ElementHandler(Session));
        }

        public override ValueTask DisposeAsync()
        {
            return new(Writer.WriteEndElementAsync());
        }
    }

    abstract class SynchronizedHandler : PayloadHandler
    {
        protected SynchronizedHandler(XmppXmlSession session) : base(session)
        {

        }

        protected abstract ValueTask AcquireImpl();

        public async ValueTask Acquire()
        {
            await Session.semaphore.WaitAsync(CancellationToken);

            try
            {
                await AcquireImpl();
            }
            catch when(OnAcquireException())
            {
                // An exception here means the handler is not returned and thus must unlock
            }

            bool OnAcquireException()
            {
                Session.semaphore.Release();
                return false;
            }
        }

        public async sealed override ValueTask DisposeAsync()
        {
            try
            {
                await base.DisposeAsync();
                await Session.FlushCommand();
            }
            finally
            {
                Session.semaphore.Release();
            }
        }
    }

    sealed class StanzaHandler : SynchronizedHandler
    {
        readonly Token<StanzaKind> kind;
        readonly Stanza stanza;

        public StanzaHandler(StanzaKind kind, in Stanza stanza, XmppXmlSession session) : base(session)
        {
            this.kind = kind.ToToken();
            this.stanza = stanza;
        }

        protected override ValueTask AcquireImpl()
        {
            return WriteStanza(kind, stanza);
        }
    }

    sealed class ElementHandler : PayloadHandler
    {
        int level = 1;

        public ElementHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<Encoder> ForkInner()
        {
            // Reuse the current instance to encode nested elements
            Interlocked.Increment(ref level);
            return new(this);
        }

        public async override ValueTask DisposeAsync()
        {
            while(Interlocked.Decrement(ref level) >= 0)
            {
                await base.DisposeAsync();
            }
        }
    }

    abstract class TopLevelElementHandler<THandler> : SynchronizedHandler where THandler : IPayloadHandler
    {
        bool acquiring;

        protected ITransportHandler Handler => this;

        public TopLevelElementHandler(XmppXmlSession session) : base(session)
        {

        }

        protected abstract ValueTask<THandler> Open();

        protected async sealed override ValueTask AcquireImpl()
        {
            acquiring = true;
            try
            {
                await Open();
            }
            finally
            {
                acquiring = false;
            }
        }

        protected sealed override ValueTask<Encoder> ForkInner()
        {
            if(acquiring)
            {
                // Called from within Acquire, unused.
                return default;
            }
            else
            {
                return base.ForkInner();
            }
        }
    }

    sealed class FeaturesHandler : TopLevelElementHandler<IFeaturesHandler>
    {
        public FeaturesHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<IFeaturesHandler> Open()
        {
            return Handler.Features();
        }
    }

    sealed class ErrorHandler : TopLevelElementHandler<IStreamErrorHandler>
    {
        public ErrorHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<IStreamErrorHandler> Open()
        {
            return Handler.Error();
        }
    }

    sealed class CompressionFailureHandler : TopLevelElementHandler<ICompressionFailureHandler>
    {
        public CompressionFailureHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<ICompressionFailureHandler> Open()
        {
            return Handler.CompressionFailure();
        }
    }

    sealed class CompressionHandler : TopLevelElementHandler<ICompressionHandler>
    {
        public CompressionHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<ICompressionHandler> Open()
        {
            return Handler.Compress();
        }
    }

    sealed class SaslFailureHandler : TopLevelElementHandler<ISaslFailureHandler>
    {
        public SaslFailureHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<ISaslFailureHandler> Open()
        {
            return Handler.SaslFailure();
        }
    }

    sealed class CommandHandler : PayloadHandler
    {
        public CommandHandler(XmppXmlSession session) : base(session)
        {

        }

        protected async override ValueTask<Encoder> ForkInner()
        {
            throw new InvalidOperationException("The command must be empty.");
        }
    }
}
