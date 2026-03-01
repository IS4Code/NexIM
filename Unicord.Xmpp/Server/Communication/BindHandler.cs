using System;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class BindHandler : CommandHandler, IBindHandler
{
    string? resource;

    public BindHandler(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask IBindHandler.Resource(string? value)
    {
        SetOnce(ref resource, value);
    }

    async ValueTask IBindHandler.Identifier(XmppResource? value)
    {
        throw XmppStanzaException.BadRequest();
    }

    public async override ValueTask DisposeAsync()
    {
        if(Session.RemoteResource != null)
        {
            // Already bound
            throw XmppStanzaException.NotAllowed();
        }

        if(!Session.IsAuthenticated)
        {
            throw XmppStanzaException.NotAuthorized();
        }

        var clientSession = Session.ClientSession;
        var accountName = clientSession.AccountName;

        // Auto-generate resource name if missing
        resource ??= Guid.NewGuid().ToString("N");

        Session.RemoteResource = new XmppResource(ClientSession.GetAddress(accountName), resource);
        clientSession.Identifier = resource;

        // TODO Handle when already exists (conflict)
        Server.Sessions.AddSession(accountName, clientSession);

        // Inform of the full resource
        await using var iq = await Session.InfoQuery(NewResponse());
        await using var bind = await iq.Bind();
        await bind.Identifier(Session.RemoteResource);
    }
}
