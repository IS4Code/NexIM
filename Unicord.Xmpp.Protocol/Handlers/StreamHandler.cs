using System;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol.Handlers;

public abstract class StreamHandler<TContext> : TransportHandler<TContext>, IStreamHandler where TContext : IPayloadHandlerContext
{
    protected virtual ValueTask<IMessageHandler?> OnMessage(in Stanza stanza)
    {
        return default;
    }

    protected virtual ValueTask<IPresenceHandler?> OnPresence(in Stanza stanza)
    {
        return default;
    }

    protected virtual ValueTask<IInfoQueryHandler?> OnInfoQuery(in Stanza stanza)
    {
        return default;
    }

    #region Implementation
    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        return StanzaHelper<IMessageHandler>.Handle(stanza, new MessageReceiver(this));
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        return StanzaHelper<IPresenceHandler>.Handle(stanza, new PresenceReceiver(this));
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        return StanzaHelper<IInfoQueryHandler>.Handle(stanza, new InfoQueryReceiver(this));
    }

    readonly struct MessageReceiver(StreamHandler<TContext> parent) : IStanzaReceiver<IMessageHandler>
    {
        public ValueTask<bool> OnEnter()
        {
            return parent.OnEnter();
        }

        public ValueTask OnExit()
        {
            return parent.OnExit();
        }

        public IMessageHandler OnExitHandler(IMessageHandler handler)
        {
            return new DelegatingMessageHandler<ExitDisposable>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IMessageHandler?> OnReceived(in Stanza stanza)
        {
            return parent.OnMessage(stanza);
        }

        public ValueTask<IMessageHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.Message(stanza);
        }
    }

    readonly struct PresenceReceiver(StreamHandler<TContext> parent) : IStanzaReceiver<IPresenceHandler>
    {
        public ValueTask<bool> OnEnter()
        {
            return parent.OnEnter();
        }

        public ValueTask OnExit()
        {
            return parent.OnExit();
        }

        public IPresenceHandler OnExitHandler(IPresenceHandler handler)
        {
            return new DelegatingPresenceHandler<ExitDisposable>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IPresenceHandler?> OnReceived(in Stanza stanza)
        {
            return parent.OnPresence(stanza);
        }

        public ValueTask<IPresenceHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.Presence(stanza);
        }
    }

    readonly struct InfoQueryReceiver(StreamHandler<TContext> parent) : IStanzaReceiver<IInfoQueryHandler>
    {
        public ValueTask<bool> OnEnter()
        {
            return parent.OnEnter();
        }

        public ValueTask OnExit()
        {
            return parent.OnExit();
        }

        public IInfoQueryHandler OnExitHandler(IInfoQueryHandler handler)
        {
            return new DelegatingInfoQueryHandler<ExitDisposable>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IInfoQueryHandler?> OnReceived(in Stanza stanza)
        {
            return parent.OnInfoQuery(stanza);
        }

        public ValueTask<IInfoQueryHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.InfoQuery(stanza);
        }
    }
    #endregion
}

public abstract class BaseStreamHandler<TContext> : BaseTransportHandler<TContext>, IStreamHandler where TContext : IPayloadHandlerContext
{
    protected abstract ValueTask<IMessageHandler?> OnMessage(in Stanza stanza);
    protected abstract ValueTask<IPresenceHandler?> OnPresence(in Stanza stanza);
    protected abstract ValueTask<IInfoQueryHandler?> OnInfoQuery(in Stanza stanza);

    #region Implementation
    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        return StanzaHelper<IMessageHandler>.Handle(stanza, new MessageReceiver(this));
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        return StanzaHelper<IPresenceHandler>.Handle(stanza, new PresenceReceiver(this));
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        return StanzaHelper<IInfoQueryHandler>.Handle(stanza, new InfoQueryReceiver(this));
    }

    readonly struct MessageReceiver(BaseStreamHandler<TContext> parent) : IStanzaReceiver<IMessageHandler>
    {
        public ValueTask<bool> OnEnter()
        {
            return parent.OnEnter();
        }

        public ValueTask OnExit()
        {
            return parent.OnExit();
        }

        public IMessageHandler OnExitHandler(IMessageHandler handler)
        {
            return new DelegatingMessageHandler<ExitDisposable>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IMessageHandler?> OnReceived(in Stanza stanza)
        {
            return parent.OnMessage(stanza);
        }

        public ValueTask<IMessageHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.Message(stanza);
        }
    }

    readonly struct PresenceReceiver(BaseStreamHandler<TContext> parent) : IStanzaReceiver<IPresenceHandler>
    {
        public ValueTask<bool> OnEnter()
        {
            return parent.OnEnter();
        }

        public ValueTask OnExit()
        {
            return parent.OnExit();
        }

        public IPresenceHandler OnExitHandler(IPresenceHandler handler)
        {
            return new DelegatingPresenceHandler<ExitDisposable>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IPresenceHandler?> OnReceived(in Stanza stanza)
        {
            return parent.OnPresence(stanza);
        }

        public ValueTask<IPresenceHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.Presence(stanza);
        }
    }

    readonly struct InfoQueryReceiver(BaseStreamHandler<TContext> parent) : IStanzaReceiver<IInfoQueryHandler>
    {
        public ValueTask<bool> OnEnter()
        {
            return parent.OnEnter();
        }

        public ValueTask OnExit()
        {
            return parent.OnExit();
        }

        public IInfoQueryHandler OnExitHandler(IInfoQueryHandler handler)
        {
            return new DelegatingInfoQueryHandler<ExitDisposable>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IInfoQueryHandler?> OnReceived(in Stanza stanza)
        {
            return parent.OnInfoQuery(stanza);
        }

        public ValueTask<IInfoQueryHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.InfoQuery(stanza);
        }
    }
    #endregion
}

file interface IStanzaReceiver<THandler> where THandler : IStanzaHandler
{
    ValueTask<bool> OnEnter();
    ValueTask OnExit();
    THandler OnExitHandler(THandler handler);
    ValueTask<THandler?> OnReceived(in Stanza stanza);
    ValueTask<THandler> Encode(in Stanza stanza, bool exit);
}

static file class StanzaHelper<TResult> where TResult : IStanzaHandler
{
    public static ValueTask<TResult> Handle<TReceiver>(in Stanza stanza, TReceiver receiver) where TReceiver : struct, IStanzaReceiver<TResult>
    {
        bool exit;
        Stanza copy;
        ValueTask<TResult?> stanzaTask;

        var enterTask = receiver.OnEnter();
        if(enterTask.IsCompletedSuccessfully)
        {
            // Entered synchronously
            exit = enterTask.Result;
            try
            {
                stanzaTask = receiver.OnReceived(stanza);
            }
            catch(Exception e)
            {
                stanzaTask = new(Task.FromException<TResult?>(e));
            }
            if(stanzaTask.IsCompletedSuccessfully && stanzaTask.Result is { } handler)
            {
                // Handled synchronously
                if(exit)
                {
                    // Result handler must be wrapped
                    return new(receiver.OnExitHandler(handler));
                }
                return new(handler);
            }
            // Not handled synchronously, use fallback
            copy = stanza;
            return WhenHandler();
        }
        else
        {
            // Wait when entered
            copy = stanza;
            return WhenEntered();
            async ValueTask<TResult> WhenEntered()
            {
                exit = await enterTask;
                try
                {
                    stanzaTask = receiver.OnReceived(copy);
                }
                catch(Exception e)
                {
                    stanzaTask = new(Task.FromException<TResult?>(e));
                }
                return await WhenHandler();
            }
        }

        async ValueTask<TResult> WhenHandler()
        {
            try
            {
                if(await stanzaTask is { } handler)
                {
                    // Handled with a delay
                    if(exit)
                    {
                        exit = false;
                        return receiver.OnExitHandler(handler);
                    }
                    return handler;
                }

                handler = await receiver.Encode(copy, exit);
                exit = false;
                return handler;
            }
            finally
            {
                if(exit)
                {
                    await receiver.OnExit();
                }
            }
        }
    }
}
