using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal sealed class GetAuthQuery : AuthQueryHandler<CommandContext>
{
    string? username;

    protected async override ValueTask OnUsername(string? value)
    {
        this.SetOnce(ref username, value);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        // Other elements are not expected
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        // TODO Consider username auth preferences (but may be empty)

        await using var iq = await this.CreateResponse();

        await using var query = await iq.AuthQuery();
        await query.Username(null);
        await query.Resource(null);

        // Never request plaintext password over insecure connection
        if(Context.Session.IsSecure)
        {
            await query.Password(null);
        }
        else
        {
            await query.Digest(null);
        }
    }
}

internal class SetAuthQuery : BaseAuthQueryHandler<CommandContext>, IDisposable
{
    string? username, resource, digest;
    TemporaryString? password;

    protected async override ValueTask OnUsername(string? value)
    {
        this.SetOnce(ref username, value);
    }

    protected async override ValueTask OnPassword(TemporaryString? value)
    {
        if(!Context.Session.IsSecure)
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
            this.SetOnce(ref password, copy);
        }
        catch when(Dispose())
        {
            // Dispose the string when not set
            throw;
        }

        bool Dispose()
        {
            copy.Dispose();
            return false;
        }
    }

    protected async override ValueTask OnDigest(string? value)
    {
        if(Context.Session.IsSecure)
        {
            // Digest was not requested
            throw XmppStanzaException.BadRequest();
        }
        this.SetOnce(ref digest, value);
    }

    protected async override ValueTask OnResource(string? value)
    {
        this.SetOnce(ref resource, value);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        // All elements must be recognized
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            if(username == null || resource == null || Context.Session.IsSecure ? password == null : digest == null)
            {
                throw XmppStanzaException.BadRequest();
            }

            if(password == null)
            {
                // Authenticating with a digest requires server knowledge of the plaintext password
                throw XmppStanzaException.NotAuthorized();
            }

            var identifier = new XmppResource(username, this.GetLocalResource().Address.Host, resource);

            var accountName = ClientSession.GetAccount(identifier, out _);

            var clientSession = new ClientSession(Context.Session)
            {
                Identifier = resource,
                AccountName = accountName
            };

            if(!await Context.Server.Authenticate(accountName, password, clientSession))
            {
                throw XmppStanzaException.NotAuthorized();
            }

            Context.Session.RemoteResource = identifier;
            Context.Session.ClientSession = clientSession;
        }
        finally
        {
            Dispose();
        }

        await this.SendResponse();
    }

    public void Dispose()
    {
        password?.Dispose();
    }
}
