using System;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class GetRosterQuery : CommandHandler, IRosterQueryHandler
{
    public GetRosterQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {
        Session.ClientSession?.SubscribeToRosterUpdates();
    }

    async ValueTask<IRosterItemHandler> IRosterQueryHandler.Item(XmppAddress? identifier, string? name, string? subscription)
    {
        throw Unexpected();
    }

    public async override ValueTask DisposeAsync()
    {
        var contacts = Account.Contacts;

        // Compute the version
        var hashCode = new HashCode();
        foreach(var contact in contacts)
        {
            hashCode.Add(contact);
        }
        var version = unchecked((uint)hashCode.ToHashCode()).ToString("x");

        await using var iq = await Session.InfoQuery(NewResponse());
        await using var roster = await iq.RosterQuery(version: version);

        foreach(var contact in contacts)
        {
            await using var item = await roster.Item(ClientSession.GetAddress(contact.Account), contact.Name, null);
            if(contact.Group is { } group)
            {
                await item.Group(group);
            }
        }
    }
}

internal class SetRosterQuery : CommandHandler, IRosterQueryHandler
{
    (XmppAddress id, string? name, bool remove)? item;
    string? group;

    public SetRosterQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask<IRosterItemHandler> IRosterQueryHandler.Item(XmppAddress? identifier, string? name, string? subscription)
    {
        if(identifier is not { } id)
        {
            throw XmppStanzaException.BadRequest("JID is missing.");
        }
        SetOnce(ref item, (id, name, subscription == "remove"));
        return new ItemHandler(this, Server, Session, null);
    }

    public async override ValueTask DisposeAsync()
    {
        if(item is not var (id, name, remove))
        {
            throw XmppStanzaException.BadRequest("Item is missing.");
        }

        var account = Account;

        var target = ClientSession.GetAccount(id);
        if(remove)
        {
            if(!await Server.RemoveContact(account, target))
            {
                throw XmppStanzaException.ItemNotFound();
            }
        }
        else
        {
            if(!await Server.SetContact(account, new Contact(target, name, group)))
            {
                throw XmppStanzaException.ItemNotFound();
            }
        }

        await using var iq = await Session.InfoQuery(NewResponse());
    }

    class ItemHandler : CommandHandler, IRosterItemHandler
    {
        readonly SetRosterQuery parent;

        public ItemHandler(SetRosterQuery parent, XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
        {
            this.parent = parent;
        }

        async ValueTask IRosterItemHandler.Group(string? name)
        {
            SetOnce(ref parent.group, name);
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
