using System.Threading.Tasks;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Formats;

namespace NexIM.Xmpp.Server.Handlers;

internal sealed class GetTime : BaseDelegatingTimeHandler<CapturingHandler<ITimeHandler>, EmptyDisposable, ICommandContext>
{
    protected sealed override CapturingHandler<ITimeHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    private TimeData GetData()
    {
        return new TimeData {
            DateTime = default,
            Extensions = InnerHandler.ToExtensions()
        };
    }

    private RetrieveEvent GetEvent()
    {
        return new RetrieveEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal abstract class DataTime : BaseDelegatingTimeHandler<TimeParser<ICommandContext>, EmptyDisposable, ICommandContext>
{
    protected sealed override TimeParser<ICommandContext> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    protected TimeData GetData()
    {
        return new TimeData {
            DateTime = InnerHandler.DateTime,
            Extensions = InnerHandler.ExtensionsHandler.ToExtensions()
        };
    }

    protected abstract QueryEvent GetEvent();

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal sealed class SetTime : DataTime
{
    protected override QueryEvent GetEvent()
    {
        return new UpdateEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}

internal sealed class ResultTime : DataTime
{
    protected override QueryEvent GetEvent()
    {
        return new ResponseEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}
