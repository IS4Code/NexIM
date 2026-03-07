using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal abstract class GetSetInfoQuery : InfoQueryHandler, IStanzaCommandHandler
{
    bool? handled;

    public required CommandState State { get; init; }
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

internal class GetServerInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    public GetServerInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

    protected async override ValueTask<IAuthQueryHandler?> OnAuthQuery()
    {
        SetHandled();
        return this.GetHandler<GetAuthQuery>();
    }

    protected async override ValueTask<IDiscoInfoQueryHandler?> OnDiscoInfoQuery(string? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ItemNotFound();
        }

        return this.GetHandler<GetServerDiscoInfoQuery>();
    }

    protected async override ValueTask<IDiscoItemsQueryHandler?> OnDiscoItemsQuery(string? node)
    {
        SetHandled();
        return this.GetHandler<GetDiscoItemsQuery>();
    }

    protected async override ValueTask<bool> OnPing()
    {
        SetHandled();

        // Sent to the server
        await this.SendResponse();
        return true;
    }

    protected async override ValueTask<IRosterQueryHandler?> OnRosterQuery(string? version)
    {
        // The server can handle the request only if it was targeted implicitly
        SetHandled();
        this.EnsureReceiverIsEmpty();
        return new GetRosterQuery(version) { State = State };
    }
}

internal class SetServerInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    public SetServerInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

    protected async override ValueTask<IAuthQueryHandler?> OnAuthQuery()
    {
        SetHandled();
        return this.GetHandler<SetAuthQuery>();
    }

    protected async override ValueTask<IBindHandler?> OnBind()
    {
        SetHandled();
        return this.GetHandler<SetBind>();
    }

    protected async override ValueTask<bool> OnSession()
    {
        SetHandled();

        // Ensure authenticated and bound
        _ = this.GetRemoteResource();

        // Success
        await this.SendResponse();
        return true;
    }

    protected async override ValueTask<bool> OnPing()
    {
        SetHandled();
        return true;
    }

    protected async override ValueTask<IRosterQueryHandler?> OnRosterQuery(string? version)
    {
        // The server can handle the request only if it was targeted implicitly
        SetHandled();
        this.EnsureReceiverIsEmpty();
        return this.GetHandler<SetRosterQuery>();
    }
}

internal class GetAccountInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    XmppAddress Address { get; }

    public GetAccountInfoQuery(in Stanza stanza) : base(stanza)
    {
        Address = (To ?? State.Session.RemoteResource)?.Address ?? throw new InvalidOperationException("Account address is missing.");
    }

    protected async override ValueTask<IDiscoInfoQueryHandler?> OnDiscoInfoQuery(string? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ItemNotFound();
        }

        return new GetAccountDiscoInfoQuery(Address) { State = State };
    }

    protected async override ValueTask<IDiscoItemsQueryHandler?> OnDiscoItemsQuery(string? node)
    {
        SetHandled();
        return this.GetHandler<GetDiscoItemsQuery>();
    }

    protected async override ValueTask<bool> OnPing()
    {
        SetHandled();

        if(State.Server.Accounts.GetAccount(ClientSession.GetAccount(Address)) != null)
        {
            // Account exists
            await this.SendResponse();
            return true;
        }
        else
        {
            throw XmppStanzaException.ServiceUnavailable();
        }
    }

    protected async override ValueTask<IRosterQueryHandler?> OnRosterQuery(string? version)
    {
        SetHandled();
        this.EnsureReceiverIsUserAccount();
        return new GetRosterQuery(version) { State = State };
    }
}

internal class SetAccountInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    public SetAccountInfoQuery(in Stanza stanza) : base(stanza)
    {

    }

    protected async override ValueTask<IRosterQueryHandler?> OnRosterQuery(string? version)
    {
        SetHandled();
        this.EnsureReceiverIsUserAccount();
        return this.GetHandler<SetRosterQuery>();
    }
}
