using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class ClientSession : IClientSession
{
    readonly IXmppSession xmpp;

    public sbyte Priority { get; set; }

    bool receivesRosterUpdates, receivesPresenceUpdates;
    ICollection<Contact>? lastUpdatedRoster;

    public string Identifier { get; }

    public Sender Sender { get; set; }
    public Status Status { get; set; }

    public ClientSession(IXmppSession xmpp, string identifier)
    {
        this.xmpp = xmpp;
        Identifier = identifier;
    }

    private string? MessageType(ConversationType? type)
    {
        return type switch
        {
            ConversationType.Normal => "normal",
            ConversationType.Chat => "chat",
            ConversationType.GroupChat => "groupchat",
            ConversationType.Headline => "headline",
            ConversationType.Error => "error",
            _ => null
        };
    }

    public void SubscribeToRosterUpdates()
    {
        receivesRosterUpdates = true;
    }

    public void SubscribeToPresenceUpdates()
    {
        receivesPresenceUpdates = true;
    }

    async ValueTask IClientSession.Conversation(Sender sender, ConversationType? type, Message? message, ChatState? chatState)
    {
        var from = GetResource(sender);

        if(message == null)
        {
            // Activity with no message
            await Notify(from, type, chatState);
            return;
        }

        await using var msg = await xmpp.Message(new Stanza(From: from, To: xmpp.RemoteResource, Type: MessageType(type)));

        await WriteSender(sender.Presentation, msg);
        if(message.Subject is { } subject)
        {
            await msg.Subject(subject);
        }
        if(message.Body is { } body)
        {
            await msg.Body(body);
        }

        switch(chatState)
        {
            case ChatState.Active:
                await msg.Active();
                break;
            case ChatState.Inactive:
                await msg.Inactive();
                break;
            case ChatState.Composing:
                await msg.Composing();
                break;
            case ChatState.Paused:
                await msg.Paused();
                break;
            case ChatState.Gone:
                await msg.Gone();
                break;
            default:
                break;
        }
    }

    private async ValueTask Notify(XmppResource from, ConversationType? type, ChatState? chatState)
    {
        switch(chatState)
        {
            case ChatState.Active:
                await using(var msg = await Write())
                {
                    await msg.Active();
                    return;
                }
            case ChatState.Inactive:
                await using(var msg = await Write())
                {
                    await msg.Inactive();
                    return;
                }
            case ChatState.Composing:
                await using(var msg = await Write())
                {
                    await msg.Composing();
                    return;
                }
            case ChatState.Paused:
                await using(var msg = await Write())
                {
                    await msg.Paused();
                    return;
                }
            case ChatState.Gone:
                await using(var msg = await Write())
                {
                    await msg.Gone();
                    return;
                }
            default:
                // Unsupported notification type does not need to cause a message
                return;
        }

        ValueTask<IMessageHandler> Write()
        {
            return xmpp.Message(new Stanza(From: from, To: xmpp.RemoteResource, Type: MessageType(type)));
        }
    }

    async ValueTask WriteSender(SenderPresentation sender, ISenderPresentation presence)
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

        var type = status.Availability == Availability.Unavailable ? "unavailable" : null;

        await using var presence = await xmpp.Presence(new Stanza(From: from, To: xmpp.RemoteResource, Type: type));

        if(status.Availability switch {
            Availability.Chatting => "chat",
            Availability.Away => "away",
            Availability.Gone => "xa",
            Availability.Busy => "dnd",
            _ => null
        } is { } show)
        {
            await presence.Show(show);
        }

        if(status.Description is { } description)
        {
            await presence.Status(description);
        }

        await WriteSender(sender.Presentation, presence);
    }

    private async ValueTask WritePresence(Sender sender, string type)
    {
        var from = GetResource(sender);

        await using var presence = await xmpp.Presence(new Stanza(From: from.Bare, To: xmpp.RemoteResource, Type: type));

        await WriteSender(sender.Presentation, presence);
    }

    async ValueTask IClientSession.SubscribeRequest(Sender sender)
    {
        await WritePresence(sender, "subscribe");
    }

    async ValueTask IClientSession.SubscribeResponse(Sender sender)
    {
        await WritePresence(sender, "subscribed");
    }

    async ValueTask IClientSession.UnsubscribeRequest(Sender sender)
    {
        await WritePresence(sender, "unsubscribe");
    }

    async ValueTask IClientSession.UnsubscribeResponse(Sender sender)
    {
        await WritePresence(sender, "unsubscribed");
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

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: "set"));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await SendContact(roster, contact);
    }

    async ValueTask IClientSession.ContactRemoved(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: "set"));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await using var item = await roster.Item(GetResource(contact.Account, null), contact.Name, "remove", null, null);
    }

    public static async ValueTask SendContact(IRosterQueryHandler roster, Contact contact)
    {
        if(!contact.SubscriptionState.ApprovedTo)
        {
            // Invisible
            return;
        }

        await using var item = await roster.Item(GetResource(contact.Account, null), contact.Name, contact.SubscriptionState.Accepted switch
        {
            SubscriptionDirection.To => "to",
            SubscriptionDirection.From => "from",
            SubscriptionDirection.Both => "both",
            _ => "none"
        }, contact.SubscriptionState.PendingTo ? "subscribe" : null, contact.SubscriptionState.ApprovedFrom);

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
