using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account
{
    private ValueTask<StatusReports> RemoveContact(AccountName target)
    {
        if(!RemoveContact(target, out var contact, out var contacts))
        {
            return new(Report(StatusCode.NotFound));
        }

        var tasks = new List<ValueTask<StatusReports>>();

        OnContactRemoved(contact, contacts, tasks);

        if(contact.SubscriptionState.AcceptedTo)
        {
            // No longer subscribing to
            tasks.Add(Post(new SubscriptionCancelledEvent {
                Origin = EventOrigin.FromTo(Name.ToIdentifier(), target.ToIdentifier()),
                Processing = EventProcessing.Create(),
                Data = null
            }));
        }
        if(contact.SubscriptionState.AcceptedFrom)
        {
            // No longer allowing subscription from
            tasks.Add(Post(new SubscriptionRejectedEvent {
                Origin = EventOrigin.FromTo(Name.ToIdentifier(), target.ToIdentifier()),
                Processing = EventProcessing.Create(),
                Data = null
            }));
        }

        return tasks.Combine();
    }

    private ValueTask<StatusReports> UpdateContact(Contact info)
    {
        var changed = SetContact(info, out _, out var updated, out var contacts);
        if(updated is null)
        {
            // Cannot add/not present
            return new(Report(StatusCode.Blocked));
        }

        if(!changed)
        {
            // Unmodified but successful
            return new(Report(StatusCode.Success));
        }

        var tasks = new List<ValueTask<StatusReports>>();
        OnContactUpdated(updated, contacts, tasks);
        return tasks.Combine();
    }

    private void OnContactUpdated(Contact contact, ICollection<Contact> contacts, List<ValueTask<StatusReports>> tasks)
    {
        tasks.Add(Save());

        RouteToAllSessions(new UpdateEvent {
            // Filled later
            Origin = EventOrigin.FromTo(Name.ToIdentifier(), default),
            Processing = EventProcessing.Create(),
            Data = new RosterUpdateData {
                Contact = contact,
                Tag = GetRosterVersionString(contacts),
                Roster = contacts
            }
        }, tasks);
    }

    private void OnContactRemoved(Contact contact, ICollection<Contact> contacts, List<ValueTask<StatusReports>> tasks)
    {
        tasks.Add(Save());

        RouteToAllSessions(new UpdateEvent {
            // Filled later
            Origin = EventOrigin.FromTo(Name.ToIdentifier(), default),
            Processing = EventProcessing.Create(),
            Data = new RosterRemoveData {
                Contact = contact,
                Tag = GetRosterVersionString(contacts),
                Roster = contacts
            }
        }, tasks);
    }

    private void OnContactUpdateOrRemoved(Contact previous, Contact? updated, ICollection<Contact> contacts, List<ValueTask<StatusReports>> tasks)
    {
        if(updated is not null)
        {
            OnContactUpdated(updated, contacts, tasks);
        }
        else
        {
            OnContactRemoved(previous, contacts, tasks);
        }
    }

    string GetRosterVersionString(ICollection<Contact> contacts)
    {
        return GetRosterVersion(contacts).ToString("x08", CultureInfo.InvariantCulture);
    }

    uint GetRosterVersion(ICollection<Contact> contacts)
    {
        // New immutable instance each time
        return unchecked((uint)contacts.GetHashCode());
    }
}
