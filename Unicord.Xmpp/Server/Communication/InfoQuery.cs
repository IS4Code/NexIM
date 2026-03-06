using System;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class GetSetInfoQuery : StanzaHandler
{
    bool? handled;

    public GetSetInfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    protected void SetHandled()
    {
        SetOnce(ref handled, true);
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
    public GetServerInfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetHandled();
        return new GetAuthQuery(Server, Session, Identifier);
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

    async ValueTask<IDiscoInfoQueryHandler> IInfoQueryHandler.DiscoInfoQuery(string? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ItemNotFound();
        }

        return new GetServerDiscoInfoQuery(Server, Session, Identifier);
    }

    async ValueTask<IDiscoItemsQueryHandler> IInfoQueryHandler.DiscoItemsQuery(string? node)
    {
        SetHandled();
        return new GetDiscoItemsQuery(Server, Session, Identifier);
    }

    async ValueTask IInfoQueryHandler.Ping()
    {
        SetHandled();

        // Sent to the server
        await using var iq = await Session.InfoQuery(NewResponse());
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery(string? version)
    {
        // The server can handle the request only if it was targeted implicitly
        SetHandled();
        EnsureReceiverIsEmpty();
        return new GetRosterQuery(Server, Session, Identifier, version);
    }
}

internal class SetServerInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    public SetServerInfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetHandled();
        return new SetAuthQuery(Server, Session, Identifier);
    }

    async ValueTask<IBindHandler> IInfoQueryHandler.Bind()
    {
        SetHandled();
        return new BindHandler(Server, Session, Identifier);
    }

    async ValueTask IInfoQueryHandler.Session()
    {
        SetHandled();

        // Ensure authenticated and bound
        _ = RemoteResource;

        // Success
        await using var iq = await Session.InfoQuery(NewResponse());
    }

    async ValueTask<IDiscoInfoQueryHandler> IInfoQueryHandler.DiscoInfoQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask<IDiscoItemsQueryHandler> IInfoQueryHandler.DiscoItemsQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask IInfoQueryHandler.Ping()
    {
        SetHandled();
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery(string? version)
    {
        // The server can handle the request only if it was targeted implicitly
        SetHandled();
        EnsureReceiverIsEmpty();
        return new SetRosterQuery(Server, Session, Identifier);
    }
}

internal class GetAccountInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    XmppAddress Address { get; }

    public GetAccountInfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {
        Address = (To ?? session.RemoteResource)?.Address ?? throw new InvalidOperationException("Account address is missing.");
    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
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

    async ValueTask<IDiscoInfoQueryHandler> IInfoQueryHandler.DiscoInfoQuery(string? node)
    {
        SetHandled();

        if(node != null)
        {
            throw XmppStanzaException.ItemNotFound();
        }

        return new GetAccountDiscoInfoQuery(Address, Server, Session, Identifier);
    }

    async ValueTask<IDiscoItemsQueryHandler> IInfoQueryHandler.DiscoItemsQuery(string? node)
    {
        SetHandled();
        return new GetDiscoItemsQuery(Server, Session, Identifier);
    }

    async ValueTask IInfoQueryHandler.Ping()
    {
        SetHandled();

        if(Server.Accounts.GetAccount(ClientSession.GetAccount(Address)) != null)
        {
            // Account exists
            await using var iq = await Session.InfoQuery(NewResponse());
        }
        else
        {
            throw XmppStanzaException.ServiceUnavailable();
        }
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery(string? version)
    {
        SetHandled();
        EnsureReceiverIsUserAccount();
        return new GetRosterQuery(Server, Session, Identifier, version);
    }
}

internal class SetAccountInfoQuery : GetSetInfoQuery, IInfoQueryHandler
{
    public SetAccountInfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
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

    async ValueTask<IDiscoInfoQueryHandler> IInfoQueryHandler.DiscoInfoQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask<IDiscoItemsQueryHandler> IInfoQueryHandler.DiscoItemsQuery(string? node)
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask IInfoQueryHandler.Ping()
    {
        SetHandled();
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery(string? version)
    {
        SetHandled();
        EnsureReceiverIsUserAccount();
        return new SetRosterQuery(Server, Session, Identifier);
    }
}
