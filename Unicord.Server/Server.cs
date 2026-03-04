using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Primitives;

namespace Unicord.Server;

public class Server
{
    public SessionsManager Sessions { get; }
    public AccountsManager Accounts { get; }

    public Server(SessionsManager sessions, AccountsManager accounts)
    {
        Sessions = sessions;
        Accounts = accounts;
    }

    public async ValueTask<bool> Authenticate(AccountName accountName, TemporaryString? password, IClientSession session)
    {
        if(!await Accounts.Authenticate(accountName, password?.Value.AsMemory() ?? default, password))
        {
            return false;
        }

        Sessions.AddSession(accountName, session);
        return true;
    }

    public async ValueTask<AccountName?> AuthenticatePlain(TemporaryUtf8String? data, Func<string, AccountName> usernameResolver)
    {
        if(data == null)
        {
            return null;
        }

        var memory = data.Value.AsMemory();

        // Format [authzid]NUL[authid]NUL[password]
        int usernameAt = memory.Span.IndexOf('\0');
        if(++usernameAt == 0)
        {
            return null;
        }
        int passwordAt = memory.Span.Slice(usernameAt).IndexOf('\0');
        if(++passwordAt == 0)
        {
            return null;
        }
        passwordAt += usernameAt;
        if(memory.Span.Slice(passwordAt).IndexOf('\0') != -1)
        {
            return null;
        }

        var authzid = memory.Slice(0, usernameAt - 1);
        var username = memory.Slice(usernameAt, passwordAt - usernameAt - 1).ToString();
        var password = memory.Slice(passwordAt);

        var accountName = usernameResolver(username);
        if(authzid.Length != 0 && !((ReadOnlySpan<char>)authzid.Span).Equals(accountName.ToString().AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if(!await Accounts.Authenticate(accountName, password, data))
        {
            return null;
        }

        return accountName;
    }

    public async ValueTask<bool> RemoveContact(Account account, AccountName target)
    {
        if(!account.RemoveContact(target, out var contact, out var contacts))
        {
            return false;
        }

        foreach(var session in Sessions.GetSessions(account.Name, null, false))
        {
            await session.ContactRemoved(contact, contacts);
        }

        var sender = new Sender(account.Name);
        if(contact.SubscriptionState.AcceptedTo)
        {
            await ReceiveUnsubscribeNotification(sender, target);
        }
        if(contact.SubscriptionState.AcceptedFrom)
        {
            await ReceiveSubscribeCancellation(sender, target);
        }

        return true;
    }

    public async ValueTask<bool> SetContact(Account account, Contact info)
    {
        var success = account.SetContact(info, out _, out var updated, out var contacts);
        if(!success || updated is null)
        {
            return false;
        }

        foreach(var session in Sessions.GetSessions(account.Name, null, false))
        {
            await session.ContactUpdated(updated, contacts);
        }

        return true;
    }

    public async ValueTask<bool> SendSubscribeRequest(Account account, SenderPresentation sender, AccountName target)
    {
        if(!account.TrySetPendingSubscriptionTo(target, out _, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        if(updated.SubscriptionState.AcceptedTo)
        {
            // Already subscribed - confirmed elsewhere
            return true;
        }

        if(!updated.SubscriptionState.PendingTo)
        {
            // Blocked for some reason
            return false;
        }

        // Inform of subscribing to contact
        await ContactUpdate(account, updated, contacts);

        // This is now an outgoing request potentially directed at another server
        // TODO Extend to use an arbitrary server
        return await ReceiveSubscribeRequest(new Sender(account.Name, Presentation: sender), target);
    }

    public async ValueTask<bool> ReceiveSubscribeRequest(Sender sender, AccountName target)
    {
        if(Accounts.GetAccount(target) is not { } targetAccount)
        {
            // Non-existent
            return false;
        }

        if(!targetAccount.TrySetPendingSubscriptionFrom(sender.Account, out _, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        if(updated.SubscriptionState.AcceptedFrom)
        {
            // Auto-accepted from approved state - update and reply back

            await ContactUpdate(targetAccount, updated, contacts);
            return await Subscribed(target, default, sender.Account);
        }

        if(!updated.SubscriptionState.PendingFrom)
        {
            // Blocked for some reason
            return false;
        }

        // Send subscription request
        bool any = false;
        foreach(var session in Sessions.GetSessions(target, null, false))
        {
            await session.SubscribeRequest(sender);
            any = true;
        }

        // Unavailable
        if(!any)
        {
            // Respond with unavailable status
            await StatusUpdate(new Sender(target), new Status(Availability.Unavailable), sender.Account);
        }
        return true;
    }

    public async ValueTask<bool> SendSubscribeResponse(Account account, SenderPresentation senderPresentation, AccountName target)
    {
        // Update confirmation
        if(!account.TrySetAcceptedSubscriptionFrom(target, out _, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        if(updated.SubscriptionState.AcceptedFrom)
        {
            // Inform of updated contact and reply back
            await ContactUpdate(account, updated, contacts);
            return await Subscribed(account.Name, senderPresentation, target);
        }

        if(!updated.SubscriptionState.ApprovedFrom)
        {
            // Blocked for some reason
            return false;
        }

        // Only approved - update contact
        await ContactUpdate(account, updated, contacts);

        return true;
    }

    public async ValueTask<bool> ReceiveSubscribeResponse(Sender sender, AccountName target)
    {
        if(Accounts.GetAccount(target) is not { } targetAccount)
        {
            return false;
        }

        if(!targetAccount.TrySetAcceptedSubscriptionTo(sender.Account, out _, out var updated, out var contacts) || !updated.SubscriptionState.AcceptedTo)
        {
            // No change
            return false;
        }

        // Inform of updated contact
        await ContactUpdate(targetAccount, updated, contacts);

        // Route to sessions
        foreach(var session in Sessions.GetSessions(target, null, false))
        {
            await session.SubscribeResponse(sender);
        }

        return true;
    }

    public async ValueTask<bool> SendSubscribeCancellation(Account account, SenderPresentation senderPresentation, AccountName target)
    {
        if(!account.TrySetCancelledSubscriptionFrom(target, out var previous, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        // Inform of updated contact
        await ContactUpdateOrRemove(account, previous, updated, contacts);

        switch(previous.SubscriptionState)
        {
            case { AcceptedFrom: true }:
                // Send as unavailable
                var status = new Status(Availability.Unavailable);
                foreach(var session in Sessions.GetSessions(account.Name, null, false))
                {
                    var sender = new Sender(account.Name, session.Identifier, senderPresentation);
                    await StatusUpdate(sender, status, target);
                }
                break;
            case { PendingFrom: true }:
                break;
            default:
                // Just unapproved
                return true;
        }

        // Route revocation
        return await ReceiveSubscribeCancellation(new Sender(account.Name, Presentation: senderPresentation), target);
    }

    public async ValueTask<bool> ReceiveSubscribeCancellation(Sender sender, AccountName target)
    {
        if(Accounts.GetAccount(target) is not { } targetAccount)
        {
            return false;
        }

        if(!targetAccount.TrySetCancelledSubscriptionTo(sender.Account, out _, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        // Route to sessions
        foreach(var session in Sessions.GetSessions(target, null, false))
        {
            await session.UnsubscribeResponse(sender);
        }

        // Inform of updated contact (must be after)
        await ContactUpdate(targetAccount, updated, contacts);

        return true;
    }

    public async ValueTask<bool> SendUnsubscribeNotification(Account account, SenderPresentation sender, AccountName target)
    {
        if(!account.TrySetCancelledSubscriptionTo(target, out _, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        // Inform of unsubscribing from contact
        await ContactUpdate(account, updated, contacts);

        // TODO Extend to use an arbitrary server
        return await ReceiveUnsubscribeNotification(new Sender(account.Name, Presentation: sender), target);
    }

    public async ValueTask<bool> ReceiveUnsubscribeNotification(Sender sender, AccountName target)
    {
        if(Accounts.GetAccount(target) is not { } targetAccount)
        {
            // Non-existent
            return false;
        }

        if(!targetAccount.TrySetCancelledSubscriptionFrom(sender.Account, out var previous, out var updated, out var contacts))
        {
            // No change
            return false;
        }

        if(!previous.SubscriptionState.AcceptedFrom)
        {
            // No action needed
            return false;
        }

        // Route to sessions
        foreach(var session in Sessions.GetSessions(target, null, false))
        {
            await session.UnsubscribeRequest(sender);
        }

        await ContactUpdateOrRemove(targetAccount, previous, updated, contacts);

        // Send as unavailable
        var status = new Status(Availability.Unavailable);
        foreach(var session in Sessions.GetSessions(target, null, false))
        {
            var targetSender = new Sender(target, session.Identifier);
            await StatusUpdate(targetSender, status, sender.Account);
        }
        return true;
    }

    private async ValueTask ContactUpdate(Account account, Contact contact, ICollection<Contact> contacts)
    {
        foreach(var session in Sessions.GetSessions(account.Name, null, false))
        {
            await session.ContactUpdated(contact, contacts);
        }
    }

    private async ValueTask ContactRemove(Account account, Contact contact, ICollection<Contact> contacts)
    {
        foreach(var session in Sessions.GetSessions(account.Name, null, false))
        {
            await session.ContactRemoved(contact, contacts);
        }
    }

    private async ValueTask ContactUpdateOrRemove(Account account, Contact previous, Contact? updated, ICollection<Contact> contacts)
    {
        if(updated is not null)
        {
            await ContactUpdate(account, updated, contacts);
        }
        else
        {
            await ContactRemove(account, previous, contacts);
        }
    }

    private async ValueTask<bool> Subscribed(AccountName account, SenderPresentation senderPresentation, AccountName target)
    {
        await ReceiveSubscribeResponse(new Sender(account, Presentation: senderPresentation), target);

        foreach(var session in Sessions.GetSessions(account, null, false))
        {
            var status = session.Status;
            if(status.Availability == Availability.Unavailable)
            {
                // Invisible
                continue;
            }

            var sender = new Sender(account, session.Identifier, session.Presentation);
            await StatusUpdate(sender, status, target);
        }

        return true;
    }

    public async ValueTask StatusUpdate(Account account, string? identifier, SenderPresentation senderPresentation, Status status)
    {
        var contacts = account.Contacts;

        foreach(var contact in contacts)
        {
            if(!contact.SubscriptionState.AcceptedFrom)
            {
                continue;
            }

            // Contact is subscribed

            if(identifier == null)
            {
                // Send from all sessions
                foreach(var session in Sessions.GetSessions(account.Name, null, false))
                {
                    var sender = new Sender(account.Name, session.Identifier, senderPresentation);
                    await StatusUpdate(sender, status, contact.Account);
                }
            }
            else
            {
                var sender = new Sender(account.Name, identifier, senderPresentation);
                await StatusUpdate(sender, status, contact.Account);
            }
        }
    }

    public async ValueTask StatusUpdate(Sender sender, Status status, AccountName target)
    {
        if(Accounts.GetAccount(target) is not { } targetAccount)
        {
            return;
        }

        if(targetAccount.GetContact(sender.Account) is not { SubscriptionState: { AcceptedTo: true } })
        {
            return;
        }

        foreach(var session in Sessions.GetSessions(target, null, false))
        {
            await session.StatusUpdate(sender, status);
        }
    }

    public async ValueTask SendStatusProbe(Account account, SenderPresentation senderPresentation)
    {
        var contacts = account.Contacts;

        var sender = new Sender(account.Name, Presentation: senderPresentation);

        foreach(var contact in contacts)
        {
            if(!contact.SubscriptionState.AcceptedTo)
            {
                continue;
            }

            // Subscribed to contact

            await ReceiveStatusProbe(sender, contact.Account);
        }
    }

    public async ValueTask<bool> ReceiveStatusProbe(Sender sender, AccountName targetAccount)
    {
        if(Accounts.GetAccount(targetAccount) is not { } account)
        {
            return false;
        }

        if(account.GetContact(sender.Account) is not { SubscriptionState.AcceptedFrom: true })
        {
            // Not subscribed
            await ReceiveSubscribeCancellation(new Sender(targetAccount), sender.Account);
            return false;
        }

        bool any = false;
        foreach(var session in Sessions.GetSessions(targetAccount, null, false))
        {
            var status = session.Status;
            if(status.Availability == Availability.Unavailable)
            {
                continue;
            }

            await StatusUpdate(new Sender(targetAccount, session.Identifier, session.Presentation), status, sender.Account);
            any = true;
        }

        // Unavailable
        if(!any)
        {
            // Respond with unavailable status
            await StatusUpdate(new Sender(targetAccount), new Status(Availability.Unavailable), sender.Account);
        }
        return true;
    }
}
