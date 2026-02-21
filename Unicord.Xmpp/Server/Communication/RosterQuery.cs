using System;
using System.Threading.Tasks;
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
        throw XmppStanzaException.BadRequest();
    }

    public async override ValueTask DisposeAsync()
    {
        if(Session.RemoteResource is not { } identifier)
        {
            throw XmppStanzaException.NotAuthorized();
        }

        if(Server.Accounts.GetAccount(ClientSession.GetAccount(identifier, out _)) is not { } account)
        {
            throw XmppStanzaException.NotAuthorized();
        }

        var contacts = account.Contacts;

        var hashCode = new HashCode();
        foreach(var contact in contacts)
        {
            hashCode.Add(contact);
        }

        await using var iq = await Session.InfoQuery(NewResponse());
        await using var roster = await iq.RosterQuery(version: unchecked((uint)hashCode.ToHashCode()).ToString("x"));

        foreach(var contact in contacts)
        {
            await roster.Item(ClientSession.GetAddress(contact.Account), contact.Name, null);
        }
    }
}

internal class SetRosterQuery : CommandHandler, IRosterQueryHandler
{
    (XmppAddress id, string? name, bool remove)? item;

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
        return new ItemHandler(Server, Session, null);
    }

    public async override ValueTask DisposeAsync()
    {
        if(item is not var (id, name, remove))
        {
            throw XmppStanzaException.BadRequest("Item is missing.");
        }

        if(Session.RemoteResource is not { } identifier)
        {
            throw XmppStanzaException.NotAuthorized();
        }

        if(Server.Accounts.GetAccount(ClientSession.GetAccount(identifier, out _)) is not { } account)
        {
            throw XmppStanzaException.NotAuthorized();
        }

        var target = ClientSession.GetAccount(id);
        if(remove)
        {
            if(account.RemoveContact(target) is { } contact)
            {
                foreach(var session in Server.Sessions.GetSessions(account.Name, null))
                {
                    await session.ContactRemoved(contact);
                }
            }
        }
        else
        {
            if(account.SetContact(target, name, null) is { } contact)
            {
                foreach(var session in Server.Sessions.GetSessions(account.Name, null))
                {
                    await session.ContactAdded(contact);
                }
            }
        }

        await using var iq = await Session.InfoQuery(NewResponse());
    }

    class ItemHandler : CommandHandler, IRosterItemHandler
    {
        public ItemHandler(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
        {

        }

        ValueTask IRosterItemHandler.Group(string? name)
        {
            return default;
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
