using System.Threading.Tasks;
using System;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class CommandHandler
{
    public XmppServer Server { get; }
    public IXmppSession Session { get; }
    public string? Identifier { get; }

    public CommandHandler(XmppServer server, IXmppSession session, string? identifier)
    {
        Server = server;
        Session = session;
        Identifier = identifier;
    }
}
