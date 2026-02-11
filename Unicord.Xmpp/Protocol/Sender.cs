using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Protocol;

using static XmppVocabulary;

public interface IXmppSession : IXmppHandler
{
    bool Connected { get; }
    string? StreamIdentifier { get; }
    XmppResource? LocalResource { get; }
    EndPoint? RemoteEndPoint { get; }
}

internal abstract class XmppXmlSession : IXmppSession
{
    readonly SemaphoreSlim semaphore = new(1, 1);
    readonly XmlWriter writer;

    public abstract bool Connected { get; }
    public string? StreamIdentifier { get; set; }
    public XmppResource? LocalResource { get; set; }
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
        var handler = new StanzaHandler(Iq, stanza, this);
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

    abstract class PayloadHandler : XmppEncoder
    {
        protected XmppXmlSession Session { get; }

        protected override XmlWriter Writer => Session.writer;
        protected override CancellationToken CancellationToken => Session.CancellationToken;

        public PayloadHandler(XmppXmlSession session)
        {
            Session = session;
        }

        protected sealed override ValueTask<XmppEncoder> ForkInner()
        {
            return new(new ElementHandler(Session));
        }

        public async override ValueTask DisposeAsync()
        {
            await Writer.WriteEndElementAsync();
        }
    }

    sealed class StanzaHandler : PayloadHandler
    {
        readonly string kind;
        readonly Stanza stanza;

        public StanzaHandler(string kind, in Stanza stanza, XmppXmlSession session) : base(session)
        {
            this.kind = kind;
            this.stanza = stanza;
        }

        public async ValueTask Acquire()
        {
            await Session.semaphore.WaitAsync(CancellationToken);

            try
            {
                var writer = Writer;
                await writer.WriteStartElementAsync(null, kind, JabberClientNs);

                if(stanza.Type is { } type)
                {
                    await writer.WriteAttributeStringAsync(null, Type, null, type);
                }
                if(stanza.From is { } from)
                {
                    await writer.WriteAttributeStringAsync(null, From, null, from.ToString());
                }
                if(stanza.To is { } to)
                {
                    await writer.WriteAttributeStringAsync(null, To, null, to.ToString());
                }
                if(stanza.Identifier is { } identifier)
                {
                    await writer.WriteAttributeStringAsync(null, Id, null, identifier);
                }
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

        public override async ValueTask DisposeAsync()
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

    sealed class ElementHandler : PayloadHandler
    {
        public ElementHandler(XmppXmlSession session) : base(session)
        {

        }
    }

    sealed class FeaturesHandler : PayloadHandler
    {
        public FeaturesHandler(XmppXmlSession session) : base(session)
        {

        }

        public async ValueTask Acquire()
        {
            await Session.semaphore.WaitAsync(CancellationToken);

            try
            {
                var writer = Writer;
                await writer.WriteStartElementAsync(null, XmppVocabulary.Features, StreamsNs);
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

        public async override ValueTask DisposeAsync()
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
}
