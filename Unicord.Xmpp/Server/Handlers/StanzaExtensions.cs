using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Handlers;

internal static class StanzaExtensions
{
    public static (StanzaType? type, XmppResource? from, XmppResource? to) OpenStanza(this IStanzaCommandHandler handler, in Stanza stanza)
    {
        var (type, from, to, _) = stanza;
        return (type?.ToEnum(), from, to);
    }

    public static void EnsureReceiverIsUserAccount(this IStanzaCommandHandler handler)
    {
        if(handler.To is { } to && to != handler.Context.Session.LocalResource?.Bare)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }

    public static void EnsureReceiverIsEmpty(this IStanzaCommandHandler handler)
    {
        if(handler.To != null)
        {
            throw XmppStanzaException.Forbidden("The receiving entity must be the user's account.");
        }
    }
}
