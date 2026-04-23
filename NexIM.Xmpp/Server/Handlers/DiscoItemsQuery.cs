using System.Threading.Tasks;
using System.Xml;
using NexIM.Xmpp.Protocol;
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

internal class GetAccountDiscoItemsQuery(XmppAddress address) : DiscoItemsQueryHandler<ICommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        if(this.GetServer().GetAccount(address.ToAccountName()) is not { } account)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        await using var iq = await this.CreateResponse();
        await using var list = await iq.DiscoItemsQuery(null);

        // TODO Check permissions
        foreach(var session in account.GetSessions(false))
        {
            await list.Item(account.Name.ToResource(session.Resource), null, null);
        }
    }
}
