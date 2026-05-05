using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NexIM.Server.Events;

namespace NexIM.Server.Accounts;

partial class Account
{
    private async ValueTask<StatusReports> RemoveContact(AccountName target)
    {
        if(await Server.FindIdentity(target) is not { } id || !RemoveContact(id, out var contact, out var contacts))
        {
            return Report(StatusCode.NotFound);
        }

        var tasks = new List<ValueTask<StatusReports>>();

        OnContactRemoved(contact, contacts, tasks);

        if(contact.SubscriptionState.AcceptedTo)
        {
            // No longer subscribing to
            tasks.Add(Post(new SubscriptionCancelledEvent {
                Origin = EventOrigin.FromTo(Name.ToIdentifier(), target.ToIdentifier()),
                Processing = EventProcessing.Create(),
                Data = PresenceData.Empty
            }));
        }
        if(contact.SubscriptionState.AcceptedFrom)
        {
            // No longer allowing subscription from
            tasks.Add(Post(new SubscriptionRejectedEvent {
                Origin = EventOrigin.FromTo(Name.ToIdentifier(), target.ToIdentifier()),
                Processing = EventProcessing.Create(),
                Data = PresenceData.Empty
            }));
        }

        return await tasks.Combine();
    }

    private async ValueTask<StatusReports> UpdateContact(Contact info)
    {
        var changed = SetContact(await Server.FindOrCreateIdentity(info.Account), info, out _, out var updated, out var contacts);
        if(updated is null)
        {
            // Cannot add/not present
            return Report(StatusCode.Blocked);
        }

        if(!changed)
        {
            // Unmodified but successful
            return Report(StatusCode.Success);
        }

        var tasks = new List<ValueTask<StatusReports>>();
        OnContactUpdated(updated, contacts, tasks);
        return await tasks.Combine();
    }

    private void OnContactUpdated(Contact contact, IReadOnlyCollection<Contact> contacts, List<ValueTask<StatusReports>> tasks)
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

    private void OnContactRemoved(Contact contact, IReadOnlyCollection<Contact> contacts, List<ValueTask<StatusReports>> tasks)
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

    private void OnContactUpdateOrRemoved(Contact previous, Contact? updated, IReadOnlyCollection<Contact> contacts, List<ValueTask<StatusReports>> tasks)
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

    string GetRosterVersionString(IReadOnlyCollection<Contact> contacts)
    {
        return GetRosterVersion(contacts).ToString("x08", CultureInfo.InvariantCulture);
    }

    uint GetRosterVersion(IReadOnlyCollection<Contact> contacts)
    {
        // New immutable instance each time
        return unchecked((uint)contacts.GetHashCode());
    }
}
