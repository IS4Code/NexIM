using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class InfoQuery : StanzaHandler, IInfoQueryHandler
{
    bool? handled;

    public InfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    public async override ValueTask DisposeAsync()
    {
        if(handled != true && Type is not (StanzaType.Result or StanzaType.Error))
        {
            throw XmppStanzaException.ServiceUnavailable();
        }
    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetOnce(ref handled, true);

        // Do not pass to other entities
        EnsureReceiverIsServer();
        switch(Type)
        {
            case StanzaType.Get:
                return new GetAuthQuery(Server, Session, Identifier);
            case StanzaType.Set:
                return new SetAuthQuery(Server, Session, Identifier);
            default:
                return NullHandler.Instance;
        }
    }

    async ValueTask<IBindHandler> IInfoQueryHandler.Bind()
    {
        SetOnce(ref handled, true);

        EnsureReceiverIsServer();
        switch(Type)
        {
            case StanzaType.Get:
                throw XmppStanzaException.BadRequest();
            case StanzaType.Set:
                return new BindHandler(Server, Session, Identifier);
            default:
                return NullHandler.Instance;
        }
    }

    async ValueTask IInfoQueryHandler.Session()
    {
        SetOnce(ref handled, true);

        EnsureReceiverIsServer();
        switch(Type)
        {
            case StanzaType.Get:
                throw XmppStanzaException.BadRequest();
            case StanzaType.Set:
                break;
            default:
                return;
        }

        // Ensure authenticated and bound
        _ = RemoteResource;

        // Success
        await using var iq = await Session.InfoQuery(NewResponse());
    }

    async ValueTask<IDiscoInfoQueryHandler> IInfoQueryHandler.DiscoInfoQuery(string? node)
    {
        SetOnce(ref handled, true);

        switch(Type)
        {
            case StanzaType.Get:
                break;
            case StanzaType.Set:
                throw XmppStanzaException.BadRequest();
            default:
                return NullHandler.Instance;
        }

        if(To is not { } to)
        {
            throw XmppStanzaException.BadRequest();
        }

        if(to == Session.LocalResource)
        {
            if(node != null)
            {
                throw XmppStanzaException.ItemNotFound();
            }

            return new GetServerDiscoInfoQuery(Server, Session, Identifier);
        }
        else if(to.ResourceIdentifier == null)
        {
            if(node != null)
            {
                throw XmppStanzaException.ItemNotFound();
            }

            return new GetAccountDiscoInfoQuery(to.Address, Server, Session, Identifier);
        }
        else
        {
            // TODO Query session
            return NullHandler.Instance;
        }
    }

    async ValueTask<IDiscoItemsQueryHandler> IInfoQueryHandler.DiscoItemsQuery(string? node)
    {
        SetOnce(ref handled, true);

        switch(Type)
        {
            case StanzaType.Get:
                break;
            case StanzaType.Set:
                throw XmppStanzaException.BadRequest();
            default:
                return NullHandler.Instance;
        }

        EnsureReceiverIsServer();

        return new GetDiscoItemsQuery(Server, Session, Identifier);
    }

    async ValueTask IInfoQueryHandler.Ping()
    {
        SetOnce(ref handled, true);

        if(Type != StanzaType.Get)
        {
            return;
        }

        if(To is not { } to || to == Session.LocalResource)
        {
            // Sent to the server
            await using var iq = await Session.InfoQuery(NewResponse());
        }
        else if(to == to.Bare)
        {
            if(Server.Accounts.GetAccount(ClientSession.GetAccount(to, out _)) != null)
            {
                // Account exists
                await using var iq = await Session.InfoQuery(NewResponse());
            }
            else
            {
                throw XmppStanzaException.ServiceUnavailable();
            }
        }
        else
        {
            // Sent to a resource, never indicate whether it exists
            // TODO Route (check presence?)
        }
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery(string? version)
    {
        SetOnce(ref handled, true);

        // Only the client's account can be the target
        EnsureReceiverIsAccount();
        switch(Type)
        {
            case StanzaType.Get:
                return new GetRosterQuery(Server, Session, Identifier, version);
            case StanzaType.Set:
                return new SetRosterQuery(Server, Session, Identifier);
            default:
                return NullHandler.Instance;
        }
    }
}
