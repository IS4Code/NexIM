using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;
using Unicord.Xmpp.Server.Handlers;

namespace Unicord.Xmpp.Server;

public class XmppServer : Unicord.Server.Server, IXmppReceiver<IXmppXmlSession>
{
    public XmppServer(SessionsManager sessions, AccountsManager accounts) : base(sessions, accounts)
    {

    }

    public ValueTask<IXmppReceivingHandler> Connected(IXmppXmlSession session)
    {
        return new(new Stream() { Context = new(this, session, null) });
    }

    public ValueTask<IMessageHandler> GetMessageHandler(IXmppXmlSession session, in Stanza stanza)
    {
        return new(new Message(stanza) { Context = new(this, session, stanza.Identifier) });
    }

    public ValueTask<IPresenceHandler> GetPresenceHandler(IXmppXmlSession session, in Stanza stanza)
    {
        return new(new Presence(stanza) { Context = new(this, session, stanza.Identifier) });
    }

    public ValueTask<IInfoQueryHandler> GetInfoQueryHandler(IXmppXmlSession session, in Stanza stanza)
    {
        if(stanza.To is not { } to || to == session.LocalResource)
        {
            // Addressed to the server
            switch(stanza.Type?.ToEnum())
            {
                case StanzaType.Get:
                    // Information retrieval
                    return new(new GetServerInfoQuery(stanza) { Context = new(this, session, stanza.Identifier) });
                case StanzaType.Set:
                    // Information update
                    return new(new SetServerInfoQuery(stanza) { Context = new(this, session, stanza.Identifier) });
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
                    return new(new GetAccountInfoQuery(stanza) { Context = new(this, session, stanza.Identifier) });
                case StanzaType.Set:
                    // Information update
                    return new(new SetAccountInfoQuery(stanza) { Context = new(this, session, stanza.Identifier) });
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
