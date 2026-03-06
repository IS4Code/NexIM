using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server;

public class XmppServer : Unicord.Server.Server, IXmppReceiver<IXmppSession>
{
    public XmppServer(SessionsManager sessions, AccountsManager accounts) : base(sessions, accounts)
    {

    }

    public ValueTask<IXmppReceivingHandler> Connected(IXmppSession session)
    {
        return new(new StreamHandler(this, session));
    }

    public ValueTask<IMessageHandler> GetMessageHandler(IXmppSession session, in Stanza stanza)
    {
        return new(new Message(this, session, stanza));
    }

    public ValueTask<IPresenceHandler> GetPresenceHandler(IXmppSession session, in Stanza stanza)
    {
        return new(new Presence(this, session, stanza));
    }

    public ValueTask<IInfoQueryHandler> GetInfoQueryHandler(IXmppSession session, in Stanza stanza)
    {
        if(stanza.To is not { } to || to == session.LocalResource)
        {
            // Addressed to the server
            switch(stanza.Type?.ToEnum())
            {
                case StanzaType.Get:
                    // Information retrieval
                    return new(new GetServerInfoQuery(this, session, stanza));
                case StanzaType.Set:
                    // Information update
                    return new(new SetServerInfoQuery(this, session, stanza));
                case StanzaType.Error:
                case StanzaType.Result:
                    // Response to an earlier inquiry must be handled by the session
                    return session.FinishCallback(stanza.Identifier);
                default:
                    // Ignore unknown types
                    return new(NullHandler.Instance);
            }
        }
        else if(stanza.To?.ResourceIdentifier == null)
        {
            // Addressed to an account
            switch(stanza.Type?.ToEnum())
            {
                case StanzaType.Get:
                    // Information retrieval
                    return new(new GetAccountInfoQuery(this, session, stanza));
                case StanzaType.Set:
                    // Information update
                    return new(new SetAccountInfoQuery(this, session, stanza));
                // TODO Consider responses to account's inquiries
                default:
                    // Ignore unknown types
                    return new(NullHandler.Instance);
            }
        }
        else
        {
            // TODO Dispatch to session
            return new(NullHandler.Instance);
        }
    }
}
