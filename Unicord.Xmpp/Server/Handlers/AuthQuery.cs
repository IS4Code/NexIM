using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal sealed class GetAuthQuery : AuthQueryHandler, ICommandHandler
{
    string? username;

    public CommandState State { get; init; }

    protected async override ValueTask<bool> OnUsername(string? value)
    {
        this.SetOnce(ref username, value);
        return true;
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
        if(State.Session.IsSecure)
        {
            await query.Password(null);
        }
        else
        {
            await query.Digest(null);
        }
    }
}

internal class SetAuthQuery : BaseAuthQueryHandler, ICommandHandler, IDisposable
{
    string? username, resource, digest;
    TemporaryString? password;

    public CommandState State { get; init; }

    protected async override ValueTask<bool> OnUsername(string? value)
    {
        this.SetOnce(ref username, value);
        return true;
    }

    protected async override ValueTask<bool> OnPassword(TemporaryString? value)
    {
        if(!State.Session.IsSecure)
        {
            // Password was not requested
            throw XmppStanzaException.BadRequest();
        }

        if(value == null)
        {
            return true;
        }

        var copy = TemporaryString.MoveFrom(value);

        try
        {
            this.SetOnce(ref password, copy);
            return true;
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

    protected async override ValueTask<bool> OnDigest(string? value)
    {
        if(State.Session.IsSecure)
        {
            // Digest was not requested
            throw XmppStanzaException.BadRequest();
        }
        this.SetOnce(ref digest, value);
        return true;
    }

    protected async override ValueTask<bool> OnResource(string? value)
    {
        this.SetOnce(ref resource, value);
        return true;
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
            if(username == null || resource == null || State.Session.IsSecure ? password == null : digest == null)
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

            var clientSession = new ClientSession(State.Session)
            {
                Identifier = resource,
                AccountName = accountName
            };

            if(!await State.Server.Authenticate(accountName, password, clientSession))
            {
                throw XmppStanzaException.NotAuthorized();
            }

            State.Session.RemoteResource = identifier;
            State.Session.ClientSession = clientSession;
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
