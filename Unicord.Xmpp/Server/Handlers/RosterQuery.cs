using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal class GetRosterQuery : RosterQueryHandler<CommandContext>
{
    readonly string? cachedVersion;

    public GetRosterQuery(string? version)
    {
        cachedVersion = version;
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        Context.Session.ClientSession?.SubscribeToRosterUpdates();

        var contacts = this.GetAccount().Contacts;

        // Compute the version
        var newVersion = ClientSession.GetContactsVersion(contacts);

        await using var iq = await this.CreateResponse();

        if(newVersion == cachedVersion)
        {
            // No changes
            return;
        }

        await using var roster = await iq.RosterQuery(version: newVersion);

        foreach(var contact in contacts)
        {
            await ClientSession.SendContact(roster, contact);
        }
    }
}

internal class SetRosterQuery : BaseRosterQueryHandler<CommandContext>
{
    (XmppResource id, string? name, bool remove)? item;
    string? group;

    protected async override ValueTask<IRosterItemHandler> OnItem(XmppResource? identifier, string? name, Token<RosterSubscriptionDirection>? subscription, Token<RosterPendingAction>? pending, bool? subscriptionApproved)
    {
        if(identifier is not { } id)
        {
            throw XmppStanzaException.BadRequest("JID is missing.");
        }

        // TODO verify?
        id = id.Bare;

        this.SetOnce(ref item, (id, name, subscription?.ToEnum() == RosterSubscriptionDirection.Remove));
        return new ItemHandler(this);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        if(item is not var (id, name, remove))
        {
            throw XmppStanzaException.BadRequest("Item is missing.");
        }

        var account = this.GetAccount();

        var target = ClientSession.GetAccount(id.Address);
        if(remove)
        {
            if(!await Context.Server.RemoveContact(account, target))
            {
                throw XmppStanzaException.ItemNotFound();
            }
        }
        else
        {
            if(!await Context.Server.SetContact(account, new Contact(target, SubscriptionState.InitialApprovedTo, Name: name, Group: group)))
            {
                throw XmppStanzaException.ItemNotFound();
            }
        }

        await this.SendResponse();
    }

    sealed class ItemHandler : BaseRosterItemHandler<CommandContext>
    {
        readonly SetRosterQuery parent;

        public ItemHandler(SetRosterQuery parent)
        {
            this.parent = parent;
            Context = parent.Context;
        }

        protected async override ValueTask OnGroup(string? name)
        {
            this.SetOnce(ref parent.group, name);
        }

        protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
        {
            await this.Unrecognized(payloadReader);
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
