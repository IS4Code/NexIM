using System;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal class SetBind : BindHandler<ICommandContext>
{
    string? resource;

    protected async override ValueTask OnResource(string? value)
    {
        this.SetOnce(ref resource, value);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        var session = this.GetSession();

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
        var accountName = clientSession.Account.Name;

        // Auto-generate resource name if missing
        resource ??= Guid.NewGuid().ToString("N");
        clientSession.Bind(resource);
        session.RemoteResource = accountName.ToResource(resource);

        // Inform of the full resource
        await using var iq = await this.CreateResponse();
        await using var bind = await iq.Bind();
        await bind.Identifier(session.RemoteResource);
    }
}
