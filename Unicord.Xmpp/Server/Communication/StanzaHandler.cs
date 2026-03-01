using System.Threading.Tasks;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class StanzaHandler : CommandHandler, IStanzaHandler
{
    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    public StanzaHandler(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza.Identifier)
    {
        Token<StanzaType>? type;
        (type, From, To, _) = stanza;
        Type = type?.ToEnum();
    }

    protected void EnsureReceiverIsServer()
    {
        if(To is { } to && to != Session.LocalResource)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the server.");
        }
    }

    protected void EnsureReceiverIsAccount()
    {
        if(To is { } to && to != Session.RemoteResource?.Bare)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    ValueTask<IStanzaErrorHandler> IStanzaHandler.Error(Token<ErrorType>? type)
    {
        return Program.NotImplemented<IStanzaErrorHandler>();
    }
}
