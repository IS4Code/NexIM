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
        if(handled != true)
        {
            throw XmppStanzaException.FeatureNotImplemented();
        }
    }

    ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        SetOnce(ref handled, true);
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
        SetOnce(ref handled, true);
        return new(
            Type switch
            {
                "get" => new GetRosterQuery(Server, Session, Identifier)
                //"set" => new SetAuthQuery(Server, Session, Identifier)
            }
        );
    }
}
