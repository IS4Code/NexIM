using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal sealed class GetAuthQuery : CommandHandler, IAuthQueryHandler
{
    string? username;

    public GetAuthQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    ValueTask IAuthQueryHandler.Username(string? value)
    {
        SetOnce(ref username, value);
        return default;
    }

    // Other elements are not expected

    ValueTask IAuthQueryHandler.Password(string? value)
    {
        return Unexpected();
    }

    ValueTask IAuthQueryHandler.Digest(string? value)
    {
        return Unexpected();
    }

    ValueTask IAuthQueryHandler.Resource(string? value)
    {
        return Unexpected();
    }

    public override ValueTask Other(XElement payload)
    {
        return Unexpected();
    }

    public async override ValueTask DisposeAsync()
    {
        // TODO Consider username auth preferences (but may be empty)

        await using var iq = await Session.InfoQuery(NewResponse());

        await using var query = await iq.AuthQuery();
        await query.Username(null);
        await query.Resource(null);

        if(Session.IsSecure)
        {
            // Can authenticate with plaintext password
            await query.Password(null);
        }
        else
        {
            await query.Digest(null);
        }
    }
}

internal class SetAuthQuery : CommandHandler, IAuthQueryHandler
{
    string? username, resource;

    public SetAuthQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    ValueTask IAuthQueryHandler.Username(string? value)
    {
        SetOnce(ref username, value);
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
        SetOnce(ref resource, value);
        return default;
    }

    public async override ValueTask DisposeAsync()
    {
        // TODO Validate

        if(Session.LocalResource is { } localResource)
        {
            var identifier = new XmppResource(username, localResource.Address.Host, resource);
            Session.RemoteResource = identifier;

            var clientSession = new ClientSession(Session);
            Server.Sessions.AddSession(Session.AccountName, clientSession);
            Session.ClientSession = clientSession;
        }
        else
        {
            throw new XmppException("The remote server is not properly identified.", false);
        }

        await using var iq = await Session.InfoQuery(NewResponse());
    }
}
