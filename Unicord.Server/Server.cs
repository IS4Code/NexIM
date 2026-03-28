using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account
{
    private void ResendEvent(Event evnt, List<Identifier>? targetsList, List<ValueTask<ErrorCode>> tasks)
    {
        if(targetsList == null)
        {
            // No need to send to anyone
            tasks.Add(new(ErrorCode.Success));
            return;
        }

        // Update the event
        evnt = evnt.WithOrigin(
            evnt.Origin with
            {
                From = new(Name, null),
                To = new(targetsList)
            }
        );
        tasks.Add(server.Post(evnt));
    }

    /*
    public async ValueTask<bool> RemoveContact(Account account, AccountName target)
    {
        if(!account.RemoveContact(target, out var contact, out var contacts))
        {
            return false;
        }

        foreach(var session in account.GetSessions(null, false))
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

        foreach(var session in account.GetSessions(null, false))
        {
            await session.ContactUpdated(updated, contacts);
        }

        return true;
    }
    */

    private async ValueTask HandleOutgoingSubscriptionRequest(Identifier source, IdentifierSet targets, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(ErrorCode.NotFound));
                continue;
            }

            if(!TrySetPendingSubscriptionTo(targetAccount, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(ErrorCode.Success));
                continue;
            }

            var targetAccountIdentifier = new Identifier(targetAccount, null);

            if(updated.SubscriptionState.AcceptedTo)
            {
                // Already subscribed - confirm
                RouteToSessions(new SubscriptionAcceptedEvent
                {
                    Origin = new()
                    {
                        From = targetAccountIdentifier,
                        To = new(new Identifier(Name, null)),
                        TransactionIdentifier = null
                    },
                    Processing = EventProcessing.NewInternal(),
                    Data = null
                }, new(source), tasks);
                continue;
            }

            if(!updated.SubscriptionState.PendingTo)
            {
                // Blocked for some reason
                tasks.Add(new(ErrorCode.NotAuthorized));
                continue;
            }

            // Inform of subscribing to contact
            ContactUpdate(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(targetAccountIdentifier);
        }

        ResendEvent(evnt, targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionRequest(Identifier identifier, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(ErrorCode.InvalidRequest));
            return;
        }

        if(!TrySetPendingSubscriptionFrom(senderAccount, out _, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(ErrorCode.Success));
            return;
        }

        if(updated.SubscriptionState.AcceptedFrom)
        {
            // Auto-accepted from approved state - update and reply back

            ContactUpdate(updated, contacts, tasks);

            OnSubscribed(new(identifier), tasks);
            return;
        }

        if(!updated.SubscriptionState.PendingFrom)
        {
            // Blocked for some reason
            tasks.Add(new(ErrorCode.NotAuthorized));
            return;
        }

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        // TODO Send unavailable? (Privacy)
    }

    private async ValueTask HandleOutgoingSubscriptionAcceptation(Identifier source, IdentifierSet targets, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(ErrorCode.NotFound));
                continue;
            }

            // Update confirmation
            if(!TrySetAcceptedSubscriptionFrom(targetAccount, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(ErrorCode.Success));
                continue;
            }

            if(!updated.SubscriptionState.AcceptedFrom)
            {
                // Was not pending

                if(!updated.SubscriptionState.ApprovedFrom)
                {
                    // Blocked for some reason
                    tasks.Add(new(ErrorCode.NotAuthorized));
                    continue;
                }

                // Only approved - update contact
                ContactUpdate(updated, contacts, tasks);
                continue;
            }

            // Inform of updated contact
            ContactUpdate(updated, contacts, tasks);

            // Pass the event through
            var targetAccountIdentifier = new Identifier(targetAccount, null);
            (targetsList ??= new()).Add(targetAccountIdentifier);
        }

        ResendEvent(evnt, targetsList, tasks);
        if(targetsList != null)
        {
            OnSubscribed(new(targetsList), tasks);
        }
    }

    private async ValueTask HandleIncomingSubscriptionAcceptation(Identifier identifier, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(ErrorCode.InvalidRequest));
            return;
        }

        if(!TrySetAcceptedSubscriptionTo(senderAccount, out _, out var updated, out var contacts) || !updated.SubscriptionState.AcceptedTo)
        {
            // No change
            tasks.Add(new(ErrorCode.Success));
            return;
        }

        // Inform of updated contact
        ContactUpdate(updated, contacts, tasks);

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }
    }

    private async ValueTask HandleOutgoingSubscriptionRejection(Identifier source, IdentifierSet targets, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        List<Identifier>? unavailableList = null;
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(ErrorCode.NotFound));
                continue;
            }

            if(!TrySetCancelledSubscriptionFrom(targetAccount, out var previous, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(ErrorCode.Success));
                continue;
            }

            // Inform of updated contact
            ContactUpdateOrRemove(previous, updated, contacts, tasks);

            var targetAccountIdentifier = new Identifier(targetAccount, null);

            switch(previous.SubscriptionState)
            {
                case { AcceptedFrom: true }:
                    // Stopped allowing subscription
                    (unavailableList ??= new()).Add(targetAccountIdentifier);
                    break;
                case { PendingFrom: true }:
                    // Rejected subscription request
                    break;
                default:
                    // Just unapproved
                    tasks.Add(new(ErrorCode.Success));
                    continue;
            }

            // Pass the event through
            (targetsList ??= new()).Add(targetAccountIdentifier);
        }

        if(unavailableList != null)
        {
            OnUnsubscribed(new(unavailableList), tasks);
        }
        ResendEvent(evnt, targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionRejection(Identifier identifier, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(ErrorCode.InvalidRequest));
            return;
        }

        if(!TrySetCancelledSubscriptionTo(senderAccount, out _, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(ErrorCode.Success));
            return;
        }

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        // Inform of updated contact (must be after)
        ContactUpdate(updated, contacts, tasks);
    }

    private async ValueTask HandleOutgoingSubscriptionCancellation(Identifier source, IdentifierSet targets, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(ErrorCode.NotFound));
                continue;
            }

            if(!TrySetCancelledSubscriptionTo(targetAccount, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(ErrorCode.Success));
                continue;
            }

            // Inform of unsubscribing from contact
            ContactUpdate(updated, contacts, tasks);

            // Pass the event through
            var targetAccountIdentifier = new Identifier(targetAccount, null);
            (targetsList ??= new()).Add(targetAccountIdentifier);
        }

        ResendEvent(evnt, targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionCancellation(Identifier identifier, Event evnt, List<ValueTask<ErrorCode>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(ErrorCode.InvalidRequest));
            return;
        }

        if(!TrySetCancelledSubscriptionFrom(senderAccount, out var previous, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(ErrorCode.Success));
            return;
        }

        if(!previous.SubscriptionState.AcceptedFrom)
        {
            // No action needed
            return;
        }

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        ContactUpdateOrRemove(previous, updated, contacts, tasks);

        // Send as unavailable
        OnUnsubscribed(new(identifier), tasks);
    }

    private void OnSubscribed(IdentifierSet targets, List<ValueTask<ErrorCode>> tasks)
    {
        // Prepare status event fields
        var origin = new EventOrigin()
        {
            From = default, // Filled later
            To = targets,
            TransactionIdentifier = null
        };
        var processing = EventProcessing.NewInternal();
        foreach(var session in GetSessions(false))
        {
            var presence = session.Presence;
            if(presence.Status.Availability == Availability.Unavailable)
            {
                // Invisible
                continue;
            }

            // Send current presence
            tasks.Add(server.Post(new StatusUpdateEvent
            {
                Origin = origin with
                {
                    From = session.Identifier
                },
                Processing = processing,
                Data = presence
            }));
        }
    }

    private void OnUnsubscribed(IdentifierSet targets, List<ValueTask<ErrorCode>> tasks)
    {
        // Prepare unavailable event fields
        var origin = new EventOrigin()
        {
            From = default, // Filled later
            To = targets,
            TransactionIdentifier = null
        };
        var processing = EventProcessing.NewInternal();
        var data = new PresenceData
        {
            Presentation = default,
            Priority = null,
            Status = new Status(Availability.Unavailable)
        };
        foreach(var session in GetSessions(false))
        {
            if(session.Presence.Status.Availability == Availability.Unavailable)
            {
                // Was not announced
                continue;
            }

            // Send as unavailable
            tasks.Add(server.Post(new StatusUpdateEvent
            {
                Origin = origin with {
                    From = session.Identifier
                },
                Processing = processing,
                Data = data
            }));
        }
    }
}
