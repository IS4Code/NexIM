using System;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class GetRosterQuery : CommandHandler, IRosterQueryHandler
{
    readonly string? cachedVersion;

    public GetRosterQuery(XmppServer server, IXmppSession session, string? identifier, string? version) : base(server, session, identifier)
    {
        Session.ClientSession?.SubscribeToRosterUpdates();

        cachedVersion = version;
    }

    async ValueTask<IRosterItemHandler> IRosterQueryHandler.Item(XmppAddress? identifier, string? name, string? subscription)
    {
        throw Unexpected();
    }

    public async override ValueTask DisposeAsync()
    {
        var contacts = Account.Contacts;

        // Compute the version
        var newVersion = ClientSession.GetContactsVersion(contacts);

        await using var iq = await Session.InfoQuery(NewResponse());

        if(newVersion == cachedVersion)
        {
            // No changes
            return;
        }

        await using var roster = await iq.RosterQuery(version: newVersion);

        foreach(var contact in contacts)
        {
            await using var item = await roster.Item(ClientSession.GetAddress(contact.Account), contact.Name, contact.SubscriptionState switch {
                SubscriptionState.To => "to",
                SubscriptionState.From => "from",
                SubscriptionState.Both => "both",
                _ => "none"
            });
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
