using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;
using Unicord.Server.Model.Events;

namespace Unicord.Xmpp.Server;

public class ClientSession : IClientSession
{
    readonly IXmppSession xmpp;

    public sbyte Priority { get; set; }

    bool receivesRosterUpdates, receivesPresenceUpdates;
    ICollection<Contact>? lastUpdatedRoster;

    public required AccountName AccountName { get; init; }
    public required string? Identifier { get; set; }

    string IClientSession.Identifier => Identifier ?? throw new InvalidOperationException("The session is not bound to any resource yet.");

    public SenderPresentation Presentation { get; set; }
    public Status Status { get; set; }

    public ClientSession(IXmppSession xmpp)
    {
        this.xmpp = xmpp;
    }

    public void SubscribeToRosterUpdates()
    {
        receivesRosterUpdates = true;
    }

    public bool UpdatePresence(SenderPresentation presentation, Status status)
    {
        Presentation = presentation;
        Status = status;
        bool available = status.Availability != Availability.Unavailable;
        bool previous = receivesPresenceUpdates;
        receivesPresenceUpdates = available;
        return !previous && available;
    }

    ValueTask<ErrorCode> IEventReceiver.Receive(Event evnt)
    {
        switch(evnt)
        {
            case MessageEvent msgEvent:
                return OnMessage(msgEvent);
        }
        return default;
    }

    private async ValueTask<ErrorCode> OnMessage(MessageEvent msgEvent)
    {
        var data = msgEvent.Data;

        await using var msg = await xmpp.Message(msgEvent.ToStanza(xmpp));

        await WriteSender(data.Presentation, msg);

        foreach(var subject in data.Subject)
        {
            await msg.Subject(subject);
        }

        foreach(var ((format, language), body) in data.Body.Data)
        {
            if(format is MessageFormat.Plain)
            {
                // TODO Other formats
                await msg.Body(new((string)body, language));
            }
        }

        switch(data.State)
        {
            case ConversationState.Active:
                await msg.Active();
                break;
            case ConversationState.Inactive:
                await msg.Inactive();
                break;
            case ConversationState.Composing:
                await msg.Composing();
                break;
            case ConversationState.Paused:
                await msg.Paused();
                break;
            case ConversationState.Gone:
                await msg.Gone();
                break;
            default:
                break;
        }

        return ErrorCode.Success;
    }

    async ValueTask WriteSender(SenderPresentation sender, IPresentationHandler presence)
    {
        if(sender.Nickname is { } nick)
        {
            await presence.Nickname(nick);
        }
    }

    async ValueTask IClientSession.StatusUpdate(Sender sender, Status status)
    {
        if(!receivesPresenceUpdates)
        {
            return;
        }

        var from = GetResource(sender);

        var type = status.Availability == Availability.Unavailable ? StanzaType.Unavailable.ToToken() : (Token<StanzaType>?)null;

        await using var presence = await xmpp.Presence(new Stanza(From: from, To: xmpp.RemoteResource, Type: type));

        if(status.Availability switch {
            Availability.Chatting => StatusType.Chat.ToToken(),
            Availability.Away => StatusType.Away.ToToken(),
            Availability.Gone => StatusType.ExtendedAway.ToToken(),
            Availability.Busy => StatusType.DoNotDisturb.ToToken(),
            _ => (Token<StatusType>?)null
        } is { } show)
        {
            await presence.Show(show);
        }

        foreach(var description in status.Description)
        {
            await presence.Status(description);
        }

        await WriteSender(sender.Presentation, presence);
    }

    private async ValueTask WritePresence(Sender sender, StanzaType type)
    {
        var from = GetResource(sender);

        await using var presence = await xmpp.Presence(new Stanza(From: from.Bare, To: xmpp.RemoteResource, Type: type.ToToken()));

        await WriteSender(sender.Presentation, presence);
    }

    async ValueTask IClientSession.SubscribeRequest(Sender sender)
    {
        await WritePresence(sender, StanzaType.Subscribe);
    }

    async ValueTask IClientSession.SubscribeResponse(Sender sender)
    {
        await WritePresence(sender, StanzaType.Subscribed);
    }

    async ValueTask IClientSession.UnsubscribeRequest(Sender sender)
    {
        await WritePresence(sender, StanzaType.Unsubscribe);
    }

    async ValueTask IClientSession.UnsubscribeResponse(Sender sender)
    {
        await WritePresence(sender, StanzaType.Unsubscribed);
    }

    public static string GetContactsVersion(ICollection<Contact> contacts)
    {
        // New immutable instance each time
        return unchecked((uint)contacts.GetHashCode()).ToString("x08");
    }

    bool CheckContactUpdate(Contact contact, ICollection<Contact> current)
    {
        if(!receivesRosterUpdates)
        {
            // Not interested
            return false;
        }

        if(current == lastUpdatedRoster)
        {
            // No update
            return false;
        }

        lastUpdatedRoster = current;

        if(!contact.SubscriptionState.ApprovedTo)
        {
            // Not explicitly added
            return false;
        }

        return true;
    }

    async ValueTask IClientSession.ContactUpdated(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: StanzaType.Set.ToToken()));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await SendContact(roster, contact);
    }

    async ValueTask IClientSession.ContactRemoved(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: StanzaType.Set.ToToken()));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await using var item = await roster.Item(GetResource(contact.Account, null), contact.Name, RosterSubscriptionDirection.Remove.ToToken(), null, null);
    }

    public static async ValueTask SendContact(IRosterQueryHandler roster, Contact contact)
    {
        if(!contact.SubscriptionState.ApprovedTo)
        {
            // Invisible
            return;
        }

        await using var item = await roster.Item(GetResource(contact.Account, null), contact.Name, contact.SubscriptionState.Direction switch
        {
            SubscriptionDirection.To => RosterSubscriptionDirection.To.ToToken(),
            SubscriptionDirection.From => RosterSubscriptionDirection.From.ToToken(),
            SubscriptionDirection.Both => RosterSubscriptionDirection.Both.ToToken(),
            _ => RosterSubscriptionDirection.None.ToToken()
        }, contact.SubscriptionState.PendingTo ? RosterPendingAction.Subscription.ToToken() : null, contact.SubscriptionState.ApprovedFrom);

        if(contact.Group is { } group)
        {
            await item.Group(group);
        }
    }

    internal static XmppAddress GetAddress(AccountName account)
    {
        return account.Identifier is XmppAddress addr ? addr : XmppResource.Parse(account.ToString() ?? "").Address;
    }

    internal static XmppResource GetResource(AccountName account, string? resourceIdentifier)
    {
        return new(
            GetAddress(account),
            resourceIdentifier
        );
    }

    internal static XmppResource GetResource(Sender sender)
    {
        return GetResource(sender.Account, sender.Identifier);
    }

    internal static AccountName GetAccount(XmppAddress address)
    {
        return AccountName.Get(address);
    }

    internal static AccountName GetAccount(XmppResource resource, out string? identifier)
    {
        identifier = resource.ResourceIdentifier;
        return AccountName.Get(resource.Address);
    }
}
