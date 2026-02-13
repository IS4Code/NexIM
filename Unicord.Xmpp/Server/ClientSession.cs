using System;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class ClientSession : IClientSession
{
    readonly IXmppSession xmpp;

    public sbyte Priority { get; set; }

    string IClientSession.Identifier => xmpp.RemoteResource?.ResourceIdentifier ?? throw new InvalidOperationException();

    public ClientSession(IXmppSession xmpp)
    {
        this.xmpp = xmpp;
    }

    ValueTask IClientSession.Send(Message message)
    {
        throw new NotImplementedException();
    }
}
