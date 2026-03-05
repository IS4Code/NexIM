using System.Threading.Tasks;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class GetDiscoInfoQuery : CommandHandler, IDiscoInfoQueryHandler
{
    public GetDiscoInfoQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask IDiscoInfoQueryHandler.Feature(Token<DiscoFeature>? feature)
    {
        throw XmppStanzaException.BadRequest();
    }

    async ValueTask IDiscoInfoQueryHandler.Identity(string? name, Token<DiscoCategory>? category, Token<DiscoType>? type)
    {
        throw XmppStanzaException.BadRequest();
    }
}

internal class GetServerDiscoInfoQuery : GetDiscoInfoQuery
{
    public GetServerDiscoInfoQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await Session.InfoQuery(NewResponse());
        await using var info = await iq.DiscoInfoQuery(null);

        // Identify the server
        await info.Identity(null, DiscoCategory.Server.ToToken(), DiscoType.IM.ToToken());

        // Supported features
        await info.Feature(DiscoFeature.DiscoInfo.ToToken());
        await info.Feature(DiscoFeature.DiscoItems.ToToken());
        await info.Feature(DiscoFeature.Ping.ToToken());
    }
}

internal class GetAccountDiscoInfoQuery : GetDiscoInfoQuery
{
    readonly XmppAddress address;

    public GetAccountDiscoInfoQuery(XmppAddress address, XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {
        this.address = address;
    }

    public async override ValueTask DisposeAsync()
    {
        if(Server.Accounts.GetAccount(ClientSession.GetAccount(address)) is not { } account)
        {
            throw XmppStanzaException.ServiceUnavailable();
        }

        await using var iq = await Session.InfoQuery(NewResponse());
        await using var info = await iq.DiscoInfoQuery(null);

        // Identify the account
        await info.Identity(null, DiscoCategory.Account.ToToken(), DiscoType.Registered.ToToken());
    }
}
