using System.Threading.Tasks;
using System.Xml;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal class GetServerDiscoItemsQuery : DiscoItemsQueryHandler<ICommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await this.CreateResponse();
        await using var list = await iq.DiscoItemsQuery(null);

        // No items
    }
}

internal class GetAccountDiscoItemsQuery : DiscoItemsQueryHandler<ICommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        // TODO Only for local accounts

        await using var iq = await this.CreateResponse();
        await using var list = await iq.DiscoItemsQuery(null);

        // No items
    }
}
