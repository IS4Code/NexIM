using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal abstract class InfoQuery : BaseDelegatingInfoQueryHandler<CapturingHandler<IInfoQueryHandler>, EmptyDisposable, ICommandContext>, ICommandHandler
{
    bool? handled;

    protected sealed override CapturingHandler<IInfoQueryHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    protected DateTimeOffset ConstructedTime { get; } = DateTimeOffset.UtcNow;

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

    protected GeneralQueryData GetQuery()
    {
        return new GeneralQueryData
        {
            Extensions = new(InnerHandler.Calls.Count > 0 ? InnerHandler : null)
        };
    }

    protected EventProcessing GetProcessing()
    {
        var time = DateTimeOffset.UtcNow;
        return new() {
            Received = ConstructedTime,
            Accepted = time,
            Published = time
        };
    }

    protected abstract Event GetEvent();

    public async override ValueTask DisposeAsync()
    {
        if(handled != true && InnerHandler.Calls.Count != 1)
        {
            // No call or multiple unhandled calls
            throw XmppStanzaException.BadRequest();
        }
        if(InnerHandler.Calls.Count == 0)
        {
            return;
        }
        // General event
        this.Post(GetEvent());
    }
}

internal class ResultInfoQuery : InfoQuery
{
    protected override Event GetEvent()
    {
        return new ResponseEvent
        {
            Origin = this.GetOrigin(),
            Processing = GetProcessing(),
            Data = GetQuery()
        };
    }
}

internal class GetInfoQuery : InfoQuery
{
    protected override Event GetEvent()
    {
        return new RetrieveEvent
        {
            Origin = this.GetOrigin(),
            Processing = GetProcessing(),
            Data = GetQuery()
        };
    }

    protected async sealed override ValueTask<IAuthQueryHandler> OnAuthQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<GetAuthQuery>();
    }
}

internal class SetInfoQuery : InfoQuery
{
    protected override Event GetEvent()
    {
        return new UpdateEvent
        {
            Origin = this.GetOrigin(),
            Processing = GetProcessing(),
            Data = GetQuery()
        };
    }

    protected async sealed override ValueTask<IAuthQueryHandler> OnAuthQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<SetAuthQuery>();
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

    protected async override ValueTask OnPing()
    {
        SetHandled();

        // Sent to the server
        await this.SendResponse();
    }

    protected async override ValueTask<ITimeHandler> OnTime()
    {
        SetHandled();
        return this.GetHandler<GetTime>();
    }
}

internal class SetServerInfoQuery : SetInfoQuery, IInfoQueryHandler
{
    protected async override ValueTask OnPing()
    {
        SetHandled();
    }
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

        return new GetAccountDiscoInfoQuery(Address) { Context = Context };
    }

    protected async override ValueTask<IDiscoItemsQueryHandler> OnDiscoItemsQuery(Token<DiscoNode>? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        return new GetAccountDiscoItemsQuery(Address) { Context = Context };
    }

    protected async override ValueTask OnPing()
    {
        SetHandled();

        if(this.GetServer().GetAccount(XmppClientSession.GetAccount(Address)) != null)
        {
            // Account exists
            await this.SendResponse();
        }
        else
        {
            throw XmppStanzaException.ServiceUnavailable();
        }
    }

    protected async override ValueTask<IRosterQueryHandler> OnRosterQuery(string? version)
    {
        SetHandled();
        this.EnsureReceiverIsUserAccount();
        return new GetRosterQuery(version) { Context = Context };
    }
}

internal class SetAccountInfoQuery : SetInfoQuery, IInfoQueryHandler
{
    protected async override ValueTask<IRosterQueryHandler> OnRosterQuery(string? version)
    {
        SetHandled();
        this.EnsureReceiverIsUserAccount();
        return this.GetHandler<SetRosterQuery>();
    }
}

internal class ErrorInfoQuery : InfoQuery
{
    // TODO Error data

    protected override Event GetEvent()
    {
        return new ErrorEvent
        {
            Origin = this.GetOrigin(),
            Processing = GetProcessing(),
            Data = new ErrorData(),
            OriginalData = GetQuery()
        };
    }
}
