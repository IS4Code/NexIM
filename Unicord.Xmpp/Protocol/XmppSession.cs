using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Protocol;

public interface IXmppSession : IStanzaHandler
{
    bool Connected { get; }
    string? StreamIdentifier { get; }
    XmppResource? LocalResource { get; }
    XmppResource? RemoteResource { get; set; }
    EndPoint? RemoteEndPoint { get; }
}

internal abstract class XmppXmlSession : IXmppSession
{
    readonly SemaphoreSlim semaphore = new(1, 1);
    readonly XmlWriter writer;

    public abstract bool Connected { get; }
    public string? StreamIdentifier { get; set; }
    public XmppResource? LocalResource { get; set; }
    public XmppResource? RemoteResource { get; set; }
    public abstract EndPoint? RemoteEndPoint { get; }

    public abstract CancellationToken CancellationToken { get; }

    public XmppXmlSession(XmlWriter writer)
    {
        this.writer = writer;
    }

    public async ValueTask<IFeaturesHandler> Features()
    {
        var handler = new FeaturesHandler(this);
        await handler.Acquire();
        return handler;
    }

    public ValueTask<IMessageHandler> Message(in Stanza stanza)
    {
        var handler = new StanzaHandler(XmppVocabulary.Message, stanza, this);
        return Enter<IMessageHandler>(handler);
    }

    public ValueTask<IPresenceHandler> Presence(in Stanza stanza)
    {
        var handler = new StanzaHandler(XmppVocabulary.Presence, stanza, this);
        return Enter<IPresenceHandler>(handler);
    }

    public ValueTask<IInfoQueryHandler> InfoQuery(in Stanza stanza)
    {
        var handler = new StanzaHandler(XmppVocabulary.Iq, stanza, this);
        return Enter<IInfoQueryHandler>(handler);
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
            await message.WriteToAsync(writer, CancellationToken);
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

        protected override XmlWriter Writer => Session.writer;
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
        public FeaturesHandler(XmppXmlSession session) : base(session)
        {

        }

        protected async override ValueTask AcquireImpl()
        {
            var writer = Writer;
            await writer.WriteStartElementAsync(null, XmppVocabulary.Features, XmppVocabulary.StreamsNs);
        }
    }
}

internal class XmppTcpXmlSession : XmppXmlSession
{
    readonly TcpClient client;

    public override bool Connected => client.Connected;
    public override EndPoint? RemoteEndPoint => client.Client.RemoteEndPoint;
    public override CancellationToken CancellationToken { get; }

    public XmppTcpXmlSession(TcpClient client, XmlWriter writer, CancellationToken cancellationToken) : base(writer)
    {
        this.client = client;
        CancellationToken = cancellationToken;
    }

    public async override ValueTask DisposeAsync()
    {
        client.Dispose();
    }
}
