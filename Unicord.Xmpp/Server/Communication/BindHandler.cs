using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

internal class SetBindHandler : BindHandler, ICommandHandler
{
    string? resource;

    public required CommandState State { get; init; }

    protected async override ValueTask<bool> OnResource(string? value)
    {
        this.SetOnce(ref resource, value);
        return true;
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        var session = State.Session;

        if(session.RemoteResource != null)
        {
            // Already bound
            throw XmppStanzaException.NotAllowed();
        }

        if(!session.IsAuthenticated)
        {
            throw XmppStanzaException.NotAuthorized();
        }

        var clientSession = session.ClientSession;
        var accountName = clientSession.AccountName;

        // Auto-generate resource name if missing
        resource ??= Guid.NewGuid().ToString("N");

        session.RemoteResource = new XmppResource(ClientSession.GetAddress(accountName), resource);
        clientSession.Identifier = resource;

        // TODO Handle when already exists (conflict)
        State.Server.Sessions.AddSession(accountName, clientSession);

        // Inform of the full resource
        await using var iq = await this.CreateResponse();
        await using var bind = await iq.Bind();
        await bind.Identifier(session.RemoteResource);
    }
}
