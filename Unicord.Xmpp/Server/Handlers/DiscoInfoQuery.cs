using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal abstract class GetDiscoInfoQuery : DiscoInfoQueryHandler<CommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }
}

internal class GetServerDiscoInfoQuery : GetDiscoInfoQuery
{
    public async override ValueTask DisposeAsync()
    {
        await using var iq = await this.CreateResponse();
        await using var info = await iq.DiscoInfoQuery(null);

        // Identify the server
        await info.Identity(null, DiscoCategory.Server.ToToken(), DiscoType.IM.ToToken());

        // Supported features
        await info.Feature(DiscoFeature.DiscoInfo.ToToken());
        await info.Feature(DiscoFeature.DiscoItems.ToToken());
        await info.Feature(DiscoFeature.Ping.ToToken());
        await info.Feature(DiscoFeature.Time.ToToken());
    }
}

internal class GetAccountDiscoInfoQuery(XmppAddress address) : GetDiscoInfoQuery
{
    public async override ValueTask DisposeAsync()
    {
        if(Context.Server.Accounts.GetAccount(ClientSession.GetAccount(address)) is not { } account)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        await using var iq = await this.CreateResponse();
        await using var info = await iq.DiscoInfoQuery(null);

        // Identify the account
        await info.Identity(null, DiscoCategory.Account.ToToken(), DiscoType.Registered.ToToken());
    }
}
