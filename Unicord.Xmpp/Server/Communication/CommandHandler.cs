using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal abstract class CommandHandler : IPayloadHandler
{
    public XmppServer Server { get; }
    public IXmppSession Session { get; }
    public string? Identifier { get; }

    protected XmppResource LocalResource => Session.LocalResource ?? throw XmppStanzaException.InternalServerError("The remote server is not properly identified.");
    protected XmppResource RemoteResource => Session.RemoteResource ?? throw XmppStanzaException.NotAuthorized();
    protected AccountName AccountName => ClientSession.GetAccount(RemoteResource, out _);
    protected Account Account => Server.Accounts.GetAccount(AccountName) ?? throw XmppStanzaException.NotAuthorized();

    public CommandHandler(XmppServer server, IXmppSession session, string? identifier)
    {
        Server = server;
        Session = session;
        Identifier = identifier;
    }

    protected Stanza NewResponse(StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return new Stanza(Type: type?.ToToken(), Identifier: Identifier, From: from ?? Session.LocalResource, To: Session.RemoteResource);
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

    protected XmppStanzaException Unexpected()
    {
        return XmppStanzaException.BadRequest("Element was not expected.");
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
