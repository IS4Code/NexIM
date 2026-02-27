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
