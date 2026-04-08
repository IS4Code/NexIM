using System;
using System.Threading.Tasks;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xmpp.Protocol.Handlers;

public abstract partial class StreamHandler<TContext> : TransportHandler<TContext> where TContext : IPayloadHandlerContext
{
    protected virtual ValueTask<IMessageHandler> OnMessage(in Stanza stanza)
    {
        return DefaultImplementation<IMessageHandler>.ValueTask;
    }

    protected virtual ValueTask<IPresenceHandler> OnPresence(in Stanza stanza)
    {
        return DefaultImplementation<IPresenceHandler>.ValueTask;
    }

    protected virtual ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza)
    {
        return DefaultImplementation<IInfoQueryHandler>.ValueTask;
    }
}

public abstract partial class BaseStreamHandler<TContext> : BaseTransportHandler<TContext> where TContext : IPayloadHandlerContext
{
    protected abstract ValueTask<IMessageHandler> OnMessage(in Stanza stanza);
    protected abstract ValueTask<IPresenceHandler> OnPresence(in Stanza stanza);
    protected abstract ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza);
}

public abstract partial class BaseDelegatingStreamHandler<THandler, TDisposable, TContext> : BaseDelegatingTransportHandler<THandler, TDisposable, TContext> where THandler : IStreamHandler where TDisposable : IAsyncDisposable where TContext : IPayloadHandlerContext
{
    protected virtual ValueTask<IMessageHandler> OnMessage(in Stanza stanza)
    {
        return InnerHandler.Message(stanza);
    }

    protected virtual ValueTask<IPresenceHandler> OnPresence(in Stanza stanza)
    {
        return InnerHandler.Presence(stanza);
    }

    protected virtual ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza)
    {
        return InnerHandler.InfoQuery(stanza);
    }
}

public class DelegatingStreamHandler<THandler, TDisposable, TContext>(THandler handler, TDisposable disposable) : BaseDelegatingStreamHandler<THandler, TDisposable, TContext> where THandler : IStreamHandler where TDisposable : IAsyncDisposable where TContext : IPayloadHandlerContext
{
    protected override THandler InnerHandler => handler;
    protected override TDisposable Disposable => disposable;
}

#region Implementation
abstract partial class StreamHandler<TContext> : IStreamHandler
{
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
            return new DelegatingMessageHandler<IMessageHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IMessageHandler> OnReceived(in Stanza stanza)
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
            return new DelegatingPresenceHandler<IPresenceHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IPresenceHandler> OnReceived(in Stanza stanza)
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
            return new DelegatingInfoQueryHandler<IInfoQueryHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IInfoQueryHandler> OnReceived(in Stanza stanza)
        {
            return parent.OnInfoQuery(stanza);
        }

        public ValueTask<IInfoQueryHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.InfoQuery(stanza);
        }
    }
}

partial class BaseStreamHandler<TContext> : IStreamHandler
{
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
            return new DelegatingMessageHandler<IMessageHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IMessageHandler> OnReceived(in Stanza stanza)
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
            return new DelegatingPresenceHandler<IPresenceHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IPresenceHandler> OnReceived(in Stanza stanza)
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
            return new DelegatingInfoQueryHandler<IInfoQueryHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IInfoQueryHandler> OnReceived(in Stanza stanza)
        {
            return parent.OnInfoQuery(stanza);
        }

        public ValueTask<IInfoQueryHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.InfoQuery(stanza);
        }
    }
}

partial class BaseDelegatingStreamHandler<THandler, TDisposable, TContext> : IStreamHandler
{
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

    readonly struct MessageReceiver(BaseDelegatingStreamHandler<THandler, TDisposable, TContext> parent) : IStanzaReceiver<IMessageHandler>
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
            return new DelegatingMessageHandler<IMessageHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IMessageHandler> OnReceived(in Stanza stanza)
        {
            return parent.OnMessage(stanza);
        }

        public ValueTask<IMessageHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.Message(stanza);
        }
    }

    readonly struct PresenceReceiver(BaseDelegatingStreamHandler<THandler, TDisposable, TContext> parent) : IStanzaReceiver<IPresenceHandler>
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
            return new DelegatingPresenceHandler<IPresenceHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IPresenceHandler> OnReceived(in Stanza stanza)
        {
            return parent.OnPresence(stanza);
        }

        public ValueTask<IPresenceHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.Presence(stanza);
        }
    }

    readonly struct InfoQueryReceiver(BaseDelegatingStreamHandler<THandler, TDisposable, TContext> parent) : IStanzaReceiver<IInfoQueryHandler>
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
            return new DelegatingInfoQueryHandler<IInfoQueryHandler, ExitDisposable, EmptyPayloadHandlerContext>(handler, new ExitDisposable(parent));
        }

        public ValueTask<IInfoQueryHandler> OnReceived(in Stanza stanza)
        {
            return parent.OnInfoQuery(stanza);
        }

        public ValueTask<IInfoQueryHandler> Encode(in Stanza stanza, bool exit)
        {
            IStreamHandler encoder = parent.GetEncoder(exit);
            return encoder.InfoQuery(stanza);
        }
    }
}
#endregion

file interface IStanzaReceiver<THandler> where THandler : IStanzaHandler
{
    ValueTask<bool> OnEnter();
    ValueTask OnExit();
    THandler OnExitHandler(THandler handler);
    ValueTask<THandler> OnReceived(in Stanza stanza);
    ValueTask<THandler> Encode(in Stanza stanza, bool exit);
}

static file class StanzaHelper<TResult> where TResult : IStanzaHandler
{
    public static ValueTask<TResult> Handle<TReceiver>(in Stanza stanza, TReceiver receiver) where TReceiver : struct, IStanzaReceiver<TResult>
    {
        bool exit;
        Stanza copy;
        ValueTask<TResult> task;

        var enterTask = receiver.OnEnter();
        if(enterTask.IsCompletedSuccessfully)
        {
            // Entered synchronously
            exit = enterTask.Result;
            try
            {
                task = receiver.OnReceived(stanza);
            }
            catch(Exception e)
            {
                task = new(Task.FromException<TResult>(e));
            }
            if(!task.Equals(DefaultImplementation<TResult>.ValueTask))
            {
                // Implemented
                if(task.IsCompletedSuccessfully)
                {
                    // Handled synchronously
                    var handler = task.Result;
                    if(exit)
                    {
                        // Result handler must be wrapped
                        return new(receiver.OnExitHandler(handler));
                    }
                    return new(handler);
                }
                // Not handled synchronously
                return WhenReceived();
            }
            // Not implemented, use fallback
            try
            {
                task = receiver.Encode(stanza, exit);
            }
            catch(Exception e)
            {
                task = new(Task.FromException<TResult>(e));
            }
            return WhenEncoded();
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
                    task = receiver.OnReceived(copy);
                }
                catch(Exception e)
                {
                    task = new(Task.FromException<TResult>(e));
                }
                if(!task.Equals(DefaultImplementation<TResult>.ValueTask))
                {
                    // Implemented
                    return await WhenReceived();
                }
                return await WhenReceived();
            }
        }

        async ValueTask<TResult> WhenReceived()
        {
            try
            {
                // Handled with a delay
                var handler = await task;
                if(exit)
                {
                    exit = false;
                    return receiver.OnExitHandler(handler);
                }
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

        async ValueTask<TResult> WhenEncoded()
        {
            try
            {
                var handler = await task;
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
