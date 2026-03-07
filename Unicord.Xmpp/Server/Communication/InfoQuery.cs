using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

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
        return new GetAuthQuery() { State = State };
    }

    async ValueTask<IBindHandler> IInfoQueryHandler.Bind()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask IInfoQueryHandler.Session()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IDiscoInfoQueryHandler?> OnDiscoInfoQuery(string? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ItemNotFound();
        }

        return new GetServerDiscoInfoQuery() { State = State };
    }

    protected async override ValueTask<IDiscoItemsQueryHandler?> OnDiscoItemsQuery(string? node)
    {
        SetHandled();
        return new GetDiscoItemsQuery() { State = State };
    }

    protected async override ValueTask<bool> OnPing()
    {
        SetHandled();

        // Sent to the server
        await using var iq = await this.CreateResponse();
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
        return new SetAuthQuery() { State = State };
    }

    protected async override ValueTask<IBindHandler?> OnBind()
    {
        SetHandled();
        return new SetBindHandler() { State = State };
    }

    protected async override ValueTask<bool> OnSession()
    {
        SetHandled();

        // Ensure authenticated and bound
        _ = this.GetRemoteResource();

        // Success
        await using var iq = await this.CreateResponse();
        return true;
    }

    protected async override ValueTask<IDiscoInfoQueryHandler?> OnDiscoInfoQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IDiscoItemsQueryHandler?> OnDiscoItemsQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
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
        return new SetRosterQuery() { State = State };
    }
}

internal class GetAccountInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    XmppAddress Address { get; }

    public GetAccountInfoQuery(in Stanza stanza) : base(stanza)
    {
        Address = (To ?? State.Session.RemoteResource)?.Address ?? throw new InvalidOperationException("Account address is missing.");
    }

    protected async override ValueTask<IAuthQueryHandler?> OnAuthQuery()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IBindHandler?> OnBind()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<bool> OnSession()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
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
        return new GetDiscoItemsQuery() { State = State };
    }

    protected async override ValueTask<bool> OnPing()
    {
        SetHandled();

        if(State.Server.Accounts.GetAccount(ClientSession.GetAccount(Address)) != null)
        {
            // Account exists
            await using var iq = await this.CreateResponse();
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

    protected async override ValueTask<IAuthQueryHandler?> OnAuthQuery()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IBindHandler?> OnBind()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<bool> OnSession()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IDiscoInfoQueryHandler?> OnDiscoInfoQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IDiscoItemsQueryHandler?> OnDiscoItemsQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<bool> OnPing()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    protected async override ValueTask<IRosterQueryHandler?> OnRosterQuery(string? version)
    {
        SetHandled();
        this.EnsureReceiverIsUserAccount();
        return new SetRosterQuery() { State = State };
    }
}
