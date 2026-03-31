using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

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
        if(this.GetServer().GetAccount(XmppClientSession.GetAccount(address)) is not { } account)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        await using var iq = await this.CreateResponse();
        await using var list = await iq.DiscoItemsQuery(null);

        // TODO Check permissions
        foreach(var session in account.GetSessions(false))
        {
            await list.Item(XmppClientSession.GetResource(account.Name, session.Resource), null, null);
        }
    }
}
