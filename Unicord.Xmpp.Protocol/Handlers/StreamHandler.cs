using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol.Handlers;

public abstract class StreamHandler : TransportHandler, IStreamHandler
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

    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        var task = OnMessage(stanza);
        if(task.IsCompletedSuccessfully && task.Result != null)
        {
            // Handled immediately
            return task!;
        }

        var copy = stanza;
        return Inner();
        async ValueTask<IMessageHandler> Inner()
        {
            if(await task is { } handler)
            {
                // Handled with a delay
                return handler;
            }

            var encoder = new FallbackEncoder(this);
            IStreamHandler impl = encoder;
            return await impl.Message(copy);
        }
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        var task = OnPresence(stanza);
        if(task.IsCompletedSuccessfully && task.Result != null)
        {
            // Handled immediately
            return task!;
        }

        var copy = stanza;
        return Inner();
        async ValueTask<IPresenceHandler> Inner()
        {
            if(await task is { } handler)
            {
                // Handled with a delay
                return handler;
            }

            var encoder = new FallbackEncoder(this);
            IStreamHandler impl = encoder;
            return await impl.Presence(copy);
        }
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        var task = OnInfoQuery(stanza);
        if(task.IsCompletedSuccessfully && task.Result != null)
        {
            // Handled immediately
            return task!;
        }

        var copy = stanza;
        return Inner();
        async ValueTask<IInfoQueryHandler> Inner()
        {
            if(await task is { } handler)
            {
                // Handled with a delay
                return handler;
            }

            var encoder = new FallbackEncoder(this);
            IStreamHandler impl = encoder;
            return await impl.InfoQuery(copy);
        }
    }
}

public abstract class BaseStreamHandler : BaseTransportHandler, IStreamHandler
{
    protected abstract ValueTask<IMessageHandler?> OnMessage(in Stanza stanza);
    protected abstract ValueTask<IPresenceHandler?> OnPresence(in Stanza stanza);
    protected abstract ValueTask<IInfoQueryHandler?> OnInfoQuery(in Stanza stanza);

    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        var task = OnMessage(stanza);
        if(task.IsCompletedSuccessfully && task.Result != null)
        {
            // Handled immediately
            return task!;
        }

        var copy = stanza;
        return Inner();
        async ValueTask<IMessageHandler> Inner()
        {
            if(await task is { } handler)
            {
                // Handled with a delay
                return handler;
            }

            var encoder = new FallbackEncoder(this);
            IStreamHandler impl = encoder;
            return await impl.Message(copy);
        }
    }

    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        var task = OnPresence(stanza);
        if(task.IsCompletedSuccessfully && task.Result != null)
        {
            // Handled immediately
            return task!;
        }

        var copy = stanza;
        return Inner();
        async ValueTask<IPresenceHandler> Inner()
        {
            if(await task is { } handler)
            {
                // Handled with a delay
                return handler;
            }

            var encoder = new FallbackEncoder(this);
            IStreamHandler impl = encoder;
            return await impl.Presence(copy);
        }
    }

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        var task = OnInfoQuery(stanza);
        if(task.IsCompletedSuccessfully && task.Result != null)
        {
            // Handled immediately
            return task!;
        }

        var copy = stanza;
        return Inner();
        async ValueTask<IInfoQueryHandler> Inner()
        {
            if(await task is { } handler)
            {
                // Handled with a delay
                return handler;
            }

            var encoder = new FallbackEncoder(this);
            IStreamHandler impl = encoder;
            return await impl.InfoQuery(copy);
        }
    }
}
