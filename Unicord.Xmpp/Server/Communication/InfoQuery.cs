using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class InfoQuery : StanzaHandler, IInfoQueryHandler
{
    public InfoQuery(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    public override ValueTask DisposeAsync()
    {
        // TODO No payload or multiple payloads
        return default;
    }

    ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        return new(
            Type switch
            {
                "get" => new GetAuthQuery(Server, Session, Identifier),
                "set" => new SetAuthQuery(Server, Session, Identifier)
            }
        );
    }

    ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery()
    {
        return new(
            Type switch
            {
                "get" => new GetRosterQuery(Server, Session, Identifier)
                //"set" => new SetAuthQuery(Server, Session, Identifier)
            }
        );
    }
}
