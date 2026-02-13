using System.Threading.Tasks;
using System;
using Unicord.Xmpp.Protocol;
using System.Xml.Linq;
using Unicord.Server;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class CommandHandler : IPayloadHandler
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

    protected Stanza NewResponse(string? type = "result")
    {
        return new Stanza(Type: type, Identifier: Identifier);
    }

    protected void SetOnce<T>(ref T storage, T value)
    {
        if(storage != null)
        {
            throw new XmppException("Property set multiple times.", false);
        }
        storage = value;
    }

    protected void ValidateSender(in Stanza stanza)
    {
        if(stanza.From is { } from && !from.IsNarrowerThan(Session.RemoteResource))
        {
            throw new XmppException("Command is comming from an unauthorized sender.", false);
        }
    }

    protected AccountName GetAccount(XmppResource resource, out string? identifier)
    {
        identifier = resource.ResourceIdentifier;
        return new(resource.Address);
    }

    protected async ValueTask Unexpected()
    {
        throw new XmppException("Element was not expected.", false);
    }

    public virtual ValueTask Other(XElement payload)
    {
        Console.WriteLine("Unknown payload: " + payload);
        return default;
    }

    public abstract ValueTask DisposeAsync();
}
