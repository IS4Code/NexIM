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
        if(handled != true && Type is not ("result" or "error"))
        {
            throw XmppStanzaException.FeatureNotImplemented();
        }
    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetOnce(ref handled, true);

        // Do not pass to other entities
        EnsureReceiverIsServer();
        switch(Type)
        {
            case "get":
                return new GetAuthQuery(Server, Session, Identifier);
            case "set":
                return new SetAuthQuery(Server, Session, Identifier);
            default:
                return NullHandler.Instance;
        }
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery(string? version)
    {
        SetOnce(ref handled, true);

        // Only the client's account can be the target
        EnsureReceiverIsAccount();
        switch(Type)
        {
            case "get":
                return new GetRosterQuery(Server, Session, Identifier);
            case "set":
                return new SetRosterQuery(Server, Session, Identifier);
            default:
                return NullHandler.Instance;
        }
    }
}
