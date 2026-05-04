using System;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal abstract class InfoQuery : BaseDelegatingInfoQueryHandler<CapturingHandler<IInfoQueryHandler>, EmptyDisposable, ICommandContext>, ICommandHandler
{
    bool handled;

    protected bool Handled => handled;
    protected bool Recognized => InnerHandler.Calls.Count == 0;

    protected sealed override CapturingHandler<IInfoQueryHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    protected void SetHandled()
    {
        this.SetOnce(ref handled, true);
    }

    protected async override ValueTask OnOther(XmlReader payloadReader)
    {
        SetHandled();

        // Captured
        await base.OnOther(payloadReader);
    }

    private GeneralQueryData GetData()
    {
        return new GeneralQueryData {
            Extensions = InnerHandler.ToExtensions()
        };
    }

    protected abstract Event GetEvent(QueryData data);

    public async override ValueTask DisposeAsync()
    {
        if(!Recognized)
        {
            this.Post(GetEvent(GetData()));
        }
    }
}

internal class ResultInfoQuery : InfoQuery
{
    protected async override ValueTask<IRosterQueryHandler> OnRosterQuery(string? version)
    {
        SetHandled();
        var handler = this.GetHandler<ResultRosterQuery>();
        handler.Version = version;
        return handler;
    }

    protected async override ValueTask<IPrivateStorageHandler> OnPrivateQuery()
    {
        SetHandled();
        return this.GetHandler<ResultPrivateStorage>();
    }

    protected async override ValueTask<IVCardHandler> OnVCard()
    {
        SetHandled();
        return this.GetHandler<ResultVCardTemp>();
    }

    protected async override ValueTask<ITimeHandler> OnTime()
    {
        SetHandled();
        return this.GetHandler<ResultTime>();
    }

    protected override Event GetEvent(QueryData data)
    {
        return new ResponseEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = data
        };
    }
}

internal abstract class GetSetInfoQuery : InfoQuery
{
    protected async override ValueTask OnPing()
    {
        SetHandled();
        this.Post(GetEvent(PingData.Empty));
    }

    public async override ValueTask DisposeAsync()
    {
        if(!Handled)
        {
            throw XmppStanzaException.BadRequest();
        }
        await base.DisposeAsync();
    }
}

internal class GetInfoQuery : GetSetInfoQuery
{
    protected async override ValueTask<IRosterQueryHandler> OnRosterQuery(string? version)
    {
        SetHandled();
        var handler = this.GetHandler<GetRosterQuery>();
        handler.Version = version;
        return handler;
    }

    protected async override ValueTask<IPrivateStorageHandler> OnPrivateQuery()
    {
        SetHandled();
        return this.GetHandler<GetPrivateStorage>();
    }

    protected async override ValueTask<IVCardHandler> OnVCard()
    {
        SetHandled();
        return this.GetHandler<GetVCardTemp>();
    }

    protected async override ValueTask<ITimeHandler> OnTime()
    {
        SetHandled();
        return this.GetHandler<GetTime>();
    }

    protected override Event GetEvent(QueryData data)
    {
        return new RetrieveEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = data
        };
    }

    protected async sealed override ValueTask<IAuthQueryHandler> OnAuthQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<GetAuthQuery>();
    }

    protected async sealed override ValueTask<IRegisterQueryHandler> OnRegisterQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<GetRegisterQuery>();
    }
}

internal class SetInfoQuery : GetSetInfoQuery
{
    protected async override ValueTask<IRosterQueryHandler> OnRosterQuery(string? version)
    {
        SetHandled();
        var handler = this.GetHandler<SetRosterQuery>();
        handler.Version = version;
        return handler;
    }

    protected async override ValueTask<IPrivateStorageHandler> OnPrivateQuery()
    {
        SetHandled();
        return this.GetHandler<SetPrivateStorage>();
    }

    protected async override ValueTask<IVCardHandler> OnVCard()
    {
        SetHandled();
        return this.GetHandler<SetVCardTemp>();
    }

    protected override Event GetEvent(QueryData data)
    {
        return new UpdateEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = data
        };
    }

    protected async sealed override ValueTask<IAuthQueryHandler> OnAuthQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<SetAuthQuery>();
    }

    protected async sealed override ValueTask<IRegisterQueryHandler> OnRegisterQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<SetRegisterQuery>();
    }

    protected async sealed override ValueTask<IBindHandler> OnBind()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<SetBind>();
    }

    protected async sealed override ValueTask OnSession()
    {
        SetHandled();
        this.EnsureReceiverIsServer();

        // Ensure authenticated and bound
        _ = this.GetRemoteResource();

        // Success
        await this.SendResponse();
    }

    public async override ValueTask DisposeAsync()
    {
        if(!Handled)
        {
            throw XmppStanzaException.BadRequest();
        }
        await base.DisposeAsync();
    }
}

internal class GetServerInfoQuery : GetInfoQuery, IInfoQueryHandler
{
    protected async override ValueTask<IDiscoInfoQueryHandler> OnDiscoInfoQuery(Token<DiscoNode>? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        return this.GetHandler<GetServerDiscoInfoQuery>();
    }

    protected async override ValueTask<IDiscoItemsQueryHandler> OnDiscoItemsQuery(Token<DiscoNode>? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        return this.GetHandler<GetServerDiscoItemsQuery>();
    }
}

internal class SetServerInfoQuery : SetInfoQuery, IInfoQueryHandler
{

}

internal class GetAccountInfoQuery : GetInfoQuery, IInfoQueryHandler
{
    XmppAddress Address => (this.GetStanza().To ?? this.TryGetRemoteResource())?.Address ?? throw new InvalidOperationException("Account address is missing.");

    protected async override ValueTask<IDiscoInfoQueryHandler> OnDiscoInfoQuery(Token<DiscoNode>? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        return this.GetHandler<GetAccountDiscoInfoQuery>();
    }

    protected async override ValueTask<IDiscoItemsQueryHandler> OnDiscoItemsQuery(Token<DiscoNode>? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        return this.GetHandler<GetAccountDiscoItemsQuery>();
    }
}

internal class SetAccountInfoQuery : SetInfoQuery, IInfoQueryHandler
{

}

internal class ErrorInfoQuery : InfoQuery
{
    ErrorParser? errorParser;

    protected async sealed override ValueTask<IStanzaErrorHandler> OnError(Token<ErrorType>? type, int? code, XmppResource? by)
    {
        return this.SetOnce(ref errorParser, new(type, code, by) { Context = Context });
    }

    protected override Event GetEvent(QueryData data)
    {
        if(errorParser == null)
        {
            throw XmppStanzaException.BadRequest();
        }
        return new ErrorEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = errorParser.GetError(data)
        };
    }

    public async override ValueTask DisposeAsync()
    {
        SetHandled();
        await base.DisposeAsync();
    }
}
