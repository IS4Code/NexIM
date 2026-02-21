using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class StanzaHandler : CommandHandler, IStanzaHandler
{
    public string? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    public StanzaHandler(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza.Identifier)
    {
        (Type, From, To, _) = stanza;
    }

    protected void EnsureReceiverIsServer()
    {
        if(To is { } to && to == Session.LocalResource)
        {
            XmppStanzaException.Forbidden("The receiving entity must be the server.");
        }
    }

    protected void EnsureReceiverIsAccount()
    {
        if(To is { } to && to == Session.RemoteResource?.Bare)
        {
            XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    ValueTask<IStanzaErrorHandler> IStanzaHandler.Error(string? type)
    {
        return Program.NotImplemented<IStanzaErrorHandler>();
    }
}
