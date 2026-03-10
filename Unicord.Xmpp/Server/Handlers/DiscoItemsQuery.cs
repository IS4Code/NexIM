using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal class GetDiscoItemsQuery : DiscoItemsQueryHandler<CommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await this.CreateResponse();
        await using var list = await iq.DiscoInfoQuery(null);

        // No items
    }
}
