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
            await Post(new SubscriptionCancelledEvent {
                Origin = EventOrigin.FromTo(Name.ToIdentifier(), target.ToIdentifier()),
                Processing = EventProcessing.NewInternal(),
                Data = null
            });
        }
        if(contact.SubscriptionState.AcceptedFrom)
        {
            // No longer allowing subscription from
            await Post(new SubscriptionRejectedEvent {
                Origin = EventOrigin.FromTo(Name.ToIdentifier(), target.ToIdentifier()),
                Processing = EventProcessing.NewInternal(),
                Data = null
            });
        }

        await Server.SaveDatabase();

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

        var tasks = new List<ValueTask<StatusCode>>();
        await ContactUpdate(updated, contacts, tasks);
        await tasks.Combine();

        return true;
    }

    private async ValueTask ContactUpdate(Contact contact, ICollection<Contact> contacts, List<ValueTask<StatusCode>> tasks)
    {
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.ContactUpdated(contact, contacts));
        }

        await Server.SaveDatabase();
    }

    private async ValueTask ContactRemove(Contact contact, ICollection<Contact> contacts, List<ValueTask<StatusCode>> tasks)
    {
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.ContactRemoved(contact, contacts));
        }

        await Server.SaveDatabase();
    }

    private ValueTask ContactUpdateOrRemove(Contact previous, Contact? updated, ICollection<Contact> contacts, List<ValueTask<StatusCode>> tasks)
    {
        if(updated is not null)
        {
            return ContactUpdate(updated, contacts, tasks);
        }
        else
        {
            return ContactRemove(previous, contacts, tasks);
        }
    }
}
