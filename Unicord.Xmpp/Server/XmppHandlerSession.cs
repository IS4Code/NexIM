using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public abstract class XmppHandlerSession : XmppSession
{
    protected internal IXmppReceivingHandler MainHandler { get; set; } = DefaultHandler.Instance;
    protected internal PayloadHandlers Handlers { get; } = new();

    protected internal StanzaInfo? LastStanza { get; set; }

    protected internal record struct StanzaInfo(StanzaKind Kind, string? Identifier);

    protected internal enum StanzaKind
    {
        Message,
        Presence,
        InfoQuery
    }

    protected internal class PayloadHandlers : Stack<IPayloadHandler>, IAsyncDisposable
    {
        public THandler Get<THandler>() where THandler : IPayloadHandler
        {
            if(!this.TryPeek(out var top) || top is not THandler handler)
            {
                throw new NotSupportedException("The current payload handler does not support this element.");
            }
            return handler;
        }

        public async ValueTask DisposeAsync()
        {
            while(this.TryPop(out var top))
            {
                await top.DisposeAsync();
            }
        }
    }

    sealed class DefaultHandler : NullHandler, IXmppReceivingHandler
    {
        public static new readonly DefaultHandler Instance = new();

        private DefaultHandler()
        {

        }

        public ValueTask StreamStarted()
        {
            return default;
        }

        public ValueTask StreamStopped()
        {
            return default;
        }
    }
}
