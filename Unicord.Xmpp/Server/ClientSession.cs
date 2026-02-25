using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class ClientSession : IClientSession
{
    readonly IXmppSession xmpp;

    public sbyte Priority { get; set; }

    bool receivesRosterUpdates, receivesPresenceUpdates;
    ICollection<Contact>? lastUpdatedRoster;

    public string Identifier { get; }

    public SenderPresentation Presentation { get; set; }
    public Status Status { get; set; }

    public ClientSession(IXmppSession xmpp, string identifier)
    {
        this.xmpp = xmpp;
        Identifier = identifier;
    }

    private Token<StanzaType>? MessageType(ConversationType? type)
    {
        return type switch
        {
            ConversationType.Normal => StanzaType.Normal.ToToken(),
            ConversationType.Chat => StanzaType.Chat.ToToken(),
            ConversationType.GroupChat => StanzaType.GroupChat.ToToken(),
            ConversationType.Headline => StanzaType.Headline.ToToken(),
            ConversationType.Error => StanzaType.Error.ToToken(),
            _ => null
        };
    }

    public void SubscribeToRosterUpdates()
    {
        receivesRosterUpdates = true;
    }

    public void UpdatePresence(SenderPresentation presentation, Status status)
    {
        Presentation = presentation;
        Status = status;
        receivesPresenceUpdates = status.Availability != Availability.Unavailable;
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

        if(status.Description is { } description)
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
