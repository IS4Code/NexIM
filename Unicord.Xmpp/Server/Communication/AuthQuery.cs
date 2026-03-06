using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
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

    async ValueTask IAuthQueryHandler.Password(TemporaryString? value)
    {
        throw Unexpected();
    }

    async ValueTask IAuthQueryHandler.Digest(string? value)
    {
        throw Unexpected();
    }

    async ValueTask IAuthQueryHandler.Resource(string? value)
    {
        throw Unexpected();
    }

    public async override ValueTask Other(XmlReader payloadReader)
    {
        throw Unexpected();
    }

    public async override ValueTask DisposeAsync()
    {
        // TODO Consider username auth preferences (but may be empty)

        await using var iq = await Session.InfoQuery(NewResponse());

        await using var query = await iq.AuthQuery();
        await query.Username(null);
        await query.Resource(null);

        // Never request plaintext password over insecure connection
        if(Session.IsSecure)
        {
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
    string? username, resource, digest;
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
        if(!Session.IsSecure)
        {
            // Password was not requested
            throw XmppStanzaException.BadRequest();
        }

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

    async ValueTask IAuthQueryHandler.Digest(string? value)
    {
        if(Session.IsSecure)
        {
            // Digest was not requested
            throw XmppStanzaException.BadRequest();
        }
        SetOnce(ref digest, value);
    }

    async ValueTask IAuthQueryHandler.Resource(string? value)
    {
        SetOnce(ref resource, value);
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            if(username == null || resource == null || Session.IsSecure ? password == null : digest == null)
            {
                throw XmppStanzaException.BadRequest();
            }

            if(password == null)
            {
                // Authenticating with a digest requires server knowledge of the plaintext password
                throw XmppStanzaException.NotAuthorized();
            }

            var identifier = new XmppResource(username, LocalResource.Address.Host, resource);

            var accountName = ClientSession.GetAccount(identifier, out _);

            var clientSession = new ClientSession(Session)
            {
                Identifier = resource,
                AccountName = accountName
            };

            if(!await Server.Authenticate(accountName, password, clientSession))
            {
                throw XmppStanzaException.NotAuthorized();
            }

            Session.RemoteResource = identifier;
            Session.ClientSession = clientSession;
        }
        finally
        {
            password?.Dispose();
        }

        await using var iq = await Session.InfoQuery(NewResponse());
    }
}
