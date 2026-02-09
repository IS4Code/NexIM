using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class GetAuthQuery : CommandHandler, IAuthQueryHandler
{
    public GetAuthQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    ValueTask IAuthQueryHandler.Username(string? value)
    {
        return default;
    }

    ValueTask IAuthQueryHandler.Password(string? value)
    {
        return default;
    }

    ValueTask IAuthQueryHandler.Digest(string? value)
    {
        return default;
    }

    ValueTask IAuthQueryHandler.Resource(string? value)
    {
        return default;
    }

    ValueTask IPayloadHandler.Other(XElement payload)
    {
        return default;
    }

    public async ValueTask DisposeAsync()
    {
        await using var iq = await Session.InfoQuery(new Stanza(Type: "result", Identifier: Identifier));
        await using var query = await iq.AuthQuery();
        await query.Username(null);
        await query.Digest(null);
        await query.Resource(null);
        //await query.Password(null);
    }
}

internal class SetAuthQuery : CommandHandler, IAuthQueryHandler
{
    public SetAuthQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    ValueTask IAuthQueryHandler.Username(string? value)
    {
        return default;
    }

    ValueTask IAuthQueryHandler.Password(string? value)
    {
        return default;
    }

    ValueTask IAuthQueryHandler.Digest(string? value)
    {
        return default;
    }

    ValueTask IAuthQueryHandler.Resource(string? value)
    {
        return default;
    }

    ValueTask IPayloadHandler.Other(XElement payload)
    {
        return default;
    }

    public async ValueTask DisposeAsync()
    {
        // TODO Validate
        await using var iq = await Session.InfoQuery(new Stanza(Type: "result", Identifier: Identifier));
    }
}
