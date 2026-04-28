using System.Threading.Tasks;
using System.Xml;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal abstract class GetDiscoInfoQuery : DiscoInfoQueryHandler<ICommandContext>
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
        await info.Feature(DiscoFeature.Multicast.ToToken());
    }
}

internal class GetAccountDiscoInfoQuery : GetDiscoInfoQuery
{
    public async override ValueTask DisposeAsync()
    {
        // TODO Only for local accounts

        await using var iq = await this.CreateResponse();
        await using var info = await iq.DiscoInfoQuery(null);

        // Identify the account
        await info.Identity(null, DiscoCategory.Account.ToToken(), DiscoType.Registered.ToToken());

        // Supported features
        await info.Feature(DiscoFeature.DiscoInfo.ToToken());
        await info.Feature(DiscoFeature.DiscoItems.ToToken());
        await info.Feature(DiscoFeature.Ping.ToToken());
        await info.Feature(DiscoFeature.Time.ToToken());
    }
}
