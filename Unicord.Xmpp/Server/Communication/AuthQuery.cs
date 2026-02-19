using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Server.Primitives;
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

    ValueTask IAuthQueryHandler.Password(TemporaryString? value)
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
    TemporaryString? password;

    public SetAuthQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask IAuthQueryHandler.Username(string? value)
    {
        SetOnce(ref username, value);
    }

    async ValueTask IAuthQueryHandler.Password(TemporaryString? value)
    {
        if(value == null)
        {
            return;
        }

        var copy = TemporaryString.MoveFrom(value);

        try
        {
            SetOnce(ref password, copy);
        }
        catch when(Dispose())
        {
            // Dispose the string when not set
        }

        bool Dispose()
        {
            copy.Dispose();
            return false;
        }
    }

    ValueTask IAuthQueryHandler.Digest(string? value)
    {
        return default;
    }

    async ValueTask IAuthQueryHandler.Resource(string? value)
    {
        SetOnce(ref resource, value);
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            if(!await Server.Accounts.Authenticate(username, password))
            {
                throw XmppStanzaException.NotAuthorized();
            }

            if(Session.LocalResource is not { } localResource)
            {
                throw XmppStanzaException.InternalServerError("The remote server is not properly identified.");
            }

            var identifier = new XmppResource(username, localResource.Address.Host, resource);
            Session.RemoteResource = identifier;

            var clientSession = new ClientSession(Session);
            Server.Sessions.AddSession(Session.AccountName, clientSession);
            Session.ClientSession = clientSession;
        }
        finally
        {
            password?.Dispose();
        }

        await using var iq = await Session.InfoQuery(NewResponse());
    }
}
