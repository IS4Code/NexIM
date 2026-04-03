using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;
using Unicord.Xmpp.Server.Handlers;

namespace Unicord.Xmpp.Server;

public class XmppServer : Unicord.Server.Server, IXmppReceiver<XmppHandlerSession>
{
    public ValueTask<IXmppReceivingHandler> Connected(XmppHandlerSession session)
    {
        return new(new Stream { Context = session });
    }

    internal ValueTask<IMessageHandler> GetMessageHandler(IXmppSession session, in Stanza stanza)
    {
        if(stanza.Type == StanzaType.Error.ToToken())
        {
            return new(new ErrorMessage { Context = (ICommandContext)session });
        }
        return new(new Message { Context = (ICommandContext)session });
    }

    internal ValueTask<IPresenceHandler> GetPresenceHandler(IXmppSession session, in Stanza stanza)
    {
        if(stanza.Type == StanzaType.Error.ToToken())
        {
            return new(new ErrorPresence { Context = (ICommandContext)session });
        }
        return new(new Presence { Context = (ICommandContext)session });
    }

    internal ValueTask<IInfoQueryHandler> GetInfoQueryHandler(IXmppSession session, in Stanza stanza)
    {
        if(stanza.To == session.LocalResource)
        {
            // Addressed to the server
            switch(stanza.Type?.ToEnum())
            {
                case StanzaType.Get:
                    // Information retrieval
                    return new(new GetServerInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Set:
                    // Information update
                    return new(new SetServerInfoQuery { Context = (ICommandContext)session });
                // Response to an earlier inquiry must be handled by the session
                case StanzaType.Error:
                {
                    if(session.FinishCallback(stanza.Identifier) is { } task)
                    {
                        return task;
                    }
                    return new(new ErrorInfoQuery { Context = (ICommandContext)session });
                }
                case StanzaType.Result:
                {
                    if(session.FinishCallback(stanza.Identifier) is { } task)
                    {
                        return task;
                    }
                    return new(new ResultInfoQuery { Context = (ICommandContext)session });
                }
                default:
                    // Ignore unknown types
                    return new(NullHandler.Instance);
            }
        }
        else if(stanza.To is not { } to || to.ResourceIdentifier == null)
        {
            // Addressed to an account
            switch(stanza.Type?.ToEnum())
            {
                case StanzaType.Get:
                    // Information retrieval
                    return new(new GetAccountInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Set:
                    // Information update
                    return new(new SetAccountInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Error:
                    return new(new ErrorInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Result:
                    return new(new ResultInfoQuery { Context = (ICommandContext)session });
                // TODO Consider responses to account's inquiries
                default:
                    // Ignore unknown types
                    return new(NullHandler.Instance);
            }
        }
        else
        {
            switch(stanza.Type?.ToEnum())
            {
                case StanzaType.Get:
                    return new(new GetInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Set:
                    return new(new SetInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Error:
                    return new(new ErrorInfoQuery { Context = (ICommandContext)session });
                case StanzaType.Result:
                    return new(new ResultInfoQuery { Context = (ICommandContext)session });
                default:
                    // Ignore unknown types
                    return new(NullHandler.Instance);
            }
        }
    }
}
