using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account
{
    public async ValueTask<bool> RemoveContact(AccountName target)
    {
        if(!RemoveContact(target, out var contact, out var contacts))
        {
            return false;
        }

        foreach(var session in GetSessions(false))
        {
            await session.ContactRemoved(contact, contacts);
        }

        if(contact.SubscriptionState.AcceptedTo)
        {
            // No longer subscribing to
            await Post(new SubscriptionCancelledEvent
            {
                Origin = new()
                {
                    From = new(Name, null),
                    To = new(new Identifier(target, null)),
                    TransactionIdentifier = null,
                    TransactionLanguage = null
                },
                Processing = EventProcessing.NewInternal(),
                Data = null
            });
        }
        if(contact.SubscriptionState.AcceptedFrom)
        {
            // No longer allowing subscription from
            await Post(new SubscriptionRejectedEvent
            {
                Origin = new()
                {
                    From = new(Name, null),
                    To = new(new Identifier(target, null)),
                    TransactionIdentifier = null,
                    TransactionLanguage = null
                },
                Processing = EventProcessing.NewInternal(),
                Data = null
            });
        }

        return true;
    }

    public async ValueTask<bool> SetContact(Contact info)
    {
        var changed = SetContact(info, out _, out var updated, out var contacts);
        if(updated is null)
        {
            // Cannot add/not present
            return false;
        }

        if(!changed)
        {
            // Unmodified but successful
            return true;
        }

        foreach(var session in GetSessions(false))
        {
            await session.ContactUpdated(updated, contacts);
        }

        return true;
    }

    private void ContactUpdate(Contact contact, ICollection<Contact> contacts, List<ValueTask<ErrorCode>> tasks)
    {
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.ContactUpdated(contact, contacts));
        }
    }

    private void ContactRemove(Contact contact, ICollection<Contact> contacts, List<ValueTask<ErrorCode>> tasks)
    {
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.ContactRemoved(contact, contacts));
        }
    }

    private void ContactUpdateOrRemove(Contact previous, Contact? updated, ICollection<Contact> contacts, List<ValueTask<ErrorCode>> tasks)
    {
        if(updated is not null)
        {
            ContactUpdate(updated, contacts, tasks);
        }
        else
        {
            ContactRemove(previous, contacts, tasks);
        }
    }
}
