using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class GetDiscoItemsQuery : CommandHandler, IDiscoItemsQueryHandler
{
    public GetDiscoItemsQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask IDiscoItemsQueryHandler.Item(XmppResource? identifier, LanguageTaggedString? name, string? node)
    {
        throw XmppStanzaException.BadRequest();
    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await Session.InfoQuery(NewResponse());
        await using var list = await iq.DiscoInfoQuery(null);

        // No items
    }
}
