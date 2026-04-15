using System;
using System.Threading.Tasks;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Formats;

namespace Unicord.Xmpp.Server.Handlers;

internal sealed class GetVCardTemp : BaseDelegatingVCardHandler<CapturingHandler<IVCardHandler>, EmptyDisposable, ICommandContext>
{
    protected sealed override CapturingHandler<IVCardHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    private VCardQueryData GetData()
    {
        return new VCardQueryData {
            VCard = null,
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

internal abstract class DataVCardTemp : BaseDelegatingVCardHandler<VCardParser<ICommandContext>, EmptyDisposable, ICommandContext>
{
    protected sealed override VCardParser<ICommandContext> InnerHandler { get; } = new(new());
    protected sealed override EmptyDisposable Disposable => default;

    protected VCardQueryData GetData()
    {
        return new VCardQueryData
        {
            VCard = InnerHandler.VCard,
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

internal sealed class SetVCardTemp : DataVCardTemp
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

internal sealed class ResultVCardTemp : DataVCardTemp
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
