using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using Unicord.Primitives.Xml;
using Unicord.Server;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server;

public class XmppClientSession : ClientSession
{
    readonly IXmppSession xmpp;

    bool receivesRosterUpdates;
    ICollection<Contact>? lastUpdatedRoster;

    public XmppClientSession(Account account, string? resource, IXmppSession xmpp) : base(account, resource)
    {
        this.xmpp = xmpp;
    }

    public void SubscribeToRosterUpdates()
    {
        receivesRosterUpdates = true;
    }

    protected override ValueTask<ErrorCode> Write(Event evnt)
    {
        switch(evnt)
        {
            case MessageEvent msgEvent:
                return WriteMessage(msgEvent.ToStanza(xmpp), msgEvent, msgEvent.Data);
            case PresenceEvent presEvent:
                return WritePresence(presEvent.ToStanza(xmpp), presEvent, presEvent.Data);
        }
        return new(ErrorCode.Unrecognized);
    }

    private async ValueTask<ErrorCode> WriteMessage(Stanza stanza, Event evnt, MessageData? data)
    {
        await using var output = await xmpp.Message(stanza);

        if(data is not null)
        {
            // Basic elements

            foreach(var subject in data.Subject)
            {
                await output.Subject(subject);
            }

            foreach(var ((format, language), body) in data.Body.Data)
            {
                if(format is MessageFormat.Plain)
                {
                    // TODO Other formats
                    await output.Body(new((string)body, language));
                }
            }

            if(data.ThreadIdentifier is { } thread)
            {
                await output.Thread(thread);
            }

            // Supported extensions

            await WriteSender(data.Presentation, output);

            switch(data.State)
            {
                case ConversationState.Active:
                    await output.Active();
                    break;
                case ConversationState.Inactive:
                    await output.Inactive();
                    break;
                case ConversationState.Composing:
                    await output.Composing();
                    break;
                case ConversationState.Paused:
                    await output.Paused();
                    break;
                case ConversationState.Gone:
                    await output.Gone();
                    break;
                default:
                    break;
            }

            // General extensions

            foreach(var extension in data.Extensions)
            {
                if(extension is CapturingHandler<IMessageHandler> capture)
                {
                    await capture.Replay(output);
                }
            }
        }

        return ErrorCode.Success;
    }

    private async ValueTask<ErrorCode> WritePresence(Stanza stanza, Event evnt, PresenceData? data)
    {
        await using var output = await xmpp.Presence(stanza);

        if(data is not null)
        {
            // Basic elements

            if(data.Status.Availability.ToStatusType() is { } statusType)
            {
                await output.Show(statusType.ToToken());
            }

            foreach(var description in data.Status.Description)
            {
                await output.Status(description);
            }

            // Supported extensions

            await WriteSender(data.Presentation, output);

            // General extensions

            foreach(var extension in data.Extensions)
            {
                if(extension is CapturingHandler<IPresenceHandler> capture)
                {
                    await capture.Replay(output);
                }
            }
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

    protected async override ValueTask<ErrorCode> ContactUpdated(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return ErrorCode.Success;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: StanzaType.Set.ToToken()));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await SendContact(roster, contact);
        return ErrorCode.Success;
    }

    protected async override ValueTask<ErrorCode> ContactRemoved(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return ErrorCode.Success;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: StanzaType.Set.ToToken()));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await using var item = await roster.Item(GetResource(contact.Account, null), contact.Name, RosterSubscriptionDirection.Remove.ToToken(), null, null);
        return ErrorCode.Success;
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
