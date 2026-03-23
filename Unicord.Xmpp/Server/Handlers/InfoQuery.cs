using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal abstract class GetSetInfoQuery : InfoQueryHandler<CommandContext>, IStanzaCommandHandler
{
    bool? handled;

    public required override CommandContext Context { get => base.Context; init => base.Context = value; }
    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    public GetSetInfoQuery(in Stanza stanza)
    {
        (Type, From, To) = this.OpenStanza(stanza);
    }

    protected void SetHandled()
    {
        this.SetOnce(ref handled, true);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        if(handled != true)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }
    }
}

internal abstract class GetInfoQuery : GetSetInfoQuery
{
    public GetInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

    protected async sealed override ValueTask<IAuthQueryHandler> OnAuthQuery()
    {
        SetHandled();
        this.EnsureReceiverIsServer();
        return this.GetHandler<GetAuthQuery>();
    }
}

internal abstract class SetInfoQuery : GetSetInfoQuery
{
    public SetInfoQuery(in Stanza stanza) : base(stanza)
    {

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
    public GetServerInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

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
    public SetServerInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

    protected async override ValueTask OnPing()
    {
        SetHandled();
    }
}

internal class GetAccountInfoQuery : GetInfoQuery, IInfoQueryHandler
{
    XmppAddress Address => (To ?? Context.Session.RemoteResource)?.Address ?? throw new InvalidOperationException("Account address is missing.");

    public GetAccountInfoQuery(in Stanza stanza) : base(stanza)
    {
        
    }

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

        if(Context.Server.GetAccount(ClientSession.GetAccount(Address)) != null)
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
    public SetAccountInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

    protected async override ValueTask<IRosterQueryHandler> OnRosterQuery(string? version)
    {
        SetHandled();
        this.EnsureReceiverIsUserAccount();
        return this.GetHandler<SetRosterQuery>();
    }
}
