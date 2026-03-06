using System.Threading.Tasks;
using Unicord.Primitives.Xml;
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

    protected void EnsureReceiverIsUserAccount()
    {
        if(To is { } to && to != Session.LocalResource?.Bare)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    protected void EnsureReceiverIsEmpty()
    {
        if(To != null)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    ValueTask<IStanzaErrorHandler> IStanzaHandler.Error(Token<ErrorType>? type, int? code)
    {
        return Program.NotImplemented<IStanzaErrorHandler>();
    }
}
