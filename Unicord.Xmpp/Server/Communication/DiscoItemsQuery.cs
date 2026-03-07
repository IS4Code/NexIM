using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

internal class GetDiscoItemsQuery : DiscoItemsQueryHandler, ICommandHandler
{
    public required CommandState State { get; init; }

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
