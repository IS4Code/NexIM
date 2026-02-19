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
            throw XmppStanzaException.BadRequest("Property set multiple times.");
        }
        storage = value;
    }

    protected void ValidateSender(in Stanza stanza)
    {
        if(stanza.From is { } from && !from.IsNarrowerThan(Session.RemoteResource))
        {
            throw XmppStreamException.InvalidFrom();
        }
    }

    protected AccountName GetAccount(XmppResource resource, out string? identifier)
    {
        identifier = resource.ResourceIdentifier;
        return new(resource.Address);
    }

    protected async ValueTask Unexpected()
    {
        throw XmppStanzaException.BadRequest("Element was not expected.");
    }

    public virtual ValueTask Other(XElement payload)
    {
        lock(typeof(Console))
        {
            Console.WriteLine("Unknown payload: " + payload);
        }
        return default;
    }

    public abstract ValueTask DisposeAsync();
}
