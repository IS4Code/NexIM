using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Server;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Server;

namespace Unicord.Xmpp.Protocol;

public interface IXmppSession : IXmppHandler
{
    bool Connected { get; }
    bool IsSecure { get; }
    bool CanUpgradeTls { get; }
    EndPoint? RemoteEndPoint { get; }

    AccountName AccountName { get; }
    ClientSession? ClientSession { get; set; }
}

public abstract class XmppXmlSession : IXmppSession
{
    readonly SemaphoreSlim semaphore = new(1, 1);
    protected XmlWriter Writer { get; private set; }
    readonly CommandHandler commandHandler;

    public abstract bool Connected { get; }
    public abstract bool IsSecure { get; }
    public abstract bool CanUpgradeTls { get; }
    public string? StreamIdentifier { get; set; }
    public XmppResource? LocalResource { get; set; }
    public XmppResource? RemoteResource { get; set; }
    public abstract EndPoint? RemoteEndPoint { get; }

    public abstract CancellationToken CancellationToken { get; }

    public AccountName AccountName => new(RemoteResource?.Address ?? throw new InvalidOperationException("This session has not been authenticated."));
    public ClientSession? ClientSession { get; set; }

    public Func<Stream, ValueTask>? OnResetStream { get; set; }

    public XmppXmlSession(XmlWriter writer)
    {
        Writer = writer;
        commandHandler = new(this);
    }

    public void Reset(XmlWriter newWriter)
    {
        Writer = newWriter;
    }

    protected abstract ValueTask UpgradeTls();

    public async ValueTask<IFeaturesHandler> Features()
    {
        var handler = new FeaturesHandler(this);
        await handler.Acquire();
        return handler;
    }

    ValueTask<IMessageHandler> IStanzaHandler.Message(in Stanza stanza)
    {
        var handler = new StanzaHandler(XmppVocabulary.Message, stanza, this);
        return Enter<IMessageHandler>(handler);
    }

    ValueTask<IPresenceHandler> IStanzaHandler.Presence(in Stanza stanza)
    {
        var handler = new StanzaHandler(XmppVocabulary.Presence, stanza, this);
        return Enter<IPresenceHandler>(handler);
    }

    ValueTask<IInfoQueryHandler> IStanzaHandler.InfoQuery(in Stanza stanza)
    {
        var handler = new StanzaHandler(XmppVocabulary.Iq, stanza, this);
        return Enter<IInfoQueryHandler>(handler);
    }

    async ValueTask IStreamTlsHandler.StartTls()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            await ((IStreamTlsHandler)commandHandler).StartTls();
        }
        finally
        {
            semaphore.Release();
        }
    }

    async ValueTask IStreamTlsHandler.ProceedTls()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            await ((IStreamTlsHandler)commandHandler).ProceedTls();
            await UpgradeTls();
        }
        finally
        {
            semaphore.Release();
        }
    }

    async ValueTask IStreamTlsHandler.FailureTls()
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            try
            {
                await ((IStreamTlsHandler)commandHandler).FailureTls();
            }
            finally
            {
                Writer.Close();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async ValueTask<THandler> Enter<THandler>(StanzaHandler handler) where THandler : IPayloadHandler
    {
        await handler.Acquire();
        return (THandler)(IPayloadHandler)handler;
    }

    public async ValueTask Other(XElement message)
    {
        await semaphore.WaitAsync(CancellationToken);
        try
        {
            await message.WriteToAsync(Writer, CancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public abstract ValueTask DisposeAsync();

    abstract class PayloadHandler : XmppEncoder
    {
        protected XmppXmlSession Session { get; }

        protected override XmlWriter Writer => Session.Writer;
        protected override CancellationToken CancellationToken => Session.CancellationToken;

        public PayloadHandler(XmppXmlSession session)
        {
            Session = session;
        }

        protected override ValueTask<XmppEncoder> ForkInner()
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
            }
            finally
            {
                Session.semaphore.Release();
            }
        }
    }

    sealed class StanzaHandler : SynchronizedHandler
    {
        readonly string kind;
        readonly Stanza stanza;

        public StanzaHandler(string kind, in Stanza stanza, XmppXmlSession session) : base(session)
        {
            this.kind = kind;
            this.stanza = stanza;
        }

        protected async override ValueTask AcquireImpl()
        {
            var writer = Writer;
            await writer.WriteStartElementAsync(null, kind, XmppVocabulary.JabberClientNs);

            if(stanza.Type is { } type)
            {
                await writer.WriteAttributeStringAsync(null, XmppVocabulary.Type, null, type);
            }
            if(stanza.From is { } from)
            {
                await writer.WriteAttributeStringAsync(null, XmppVocabulary.From, null, from.ToString());
            }
            if(stanza.To is { } to)
            {
                await writer.WriteAttributeStringAsync(null, XmppVocabulary.To, null, to.ToString());
            }
            if(stanza.Identifier is { } identifier)
            {
                await writer.WriteAttributeStringAsync(null, XmppVocabulary.Id, null, identifier);
            }
        }
    }

    sealed class ElementHandler : PayloadHandler
    {
        int level = 1;

        public ElementHandler(XmppXmlSession session) : base(session)
        {

        }

        protected override ValueTask<XmppEncoder> ForkInner()
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

    sealed class FeaturesHandler : SynchronizedHandler
    {
        bool acquiring;

        public FeaturesHandler(XmppXmlSession session) : base(session)
        {

        }

        protected async override ValueTask AcquireImpl()
        {
            acquiring = true;
            try
            {
                await ((IStreamHandler)this).Features();
            }
            finally
            {
                acquiring = false;
            }
        }

        protected override ValueTask<XmppEncoder> ForkInner()
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

    sealed class CommandHandler : PayloadHandler
    {
        public CommandHandler(XmppXmlSession session) : base(session)
        {

        }

        protected async override ValueTask<XmppEncoder> ForkInner()
        {
            throw new InvalidOperationException("The command must be empty.");
        }
    }
}
