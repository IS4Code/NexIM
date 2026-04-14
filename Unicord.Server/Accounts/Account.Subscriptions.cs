using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account
{
    private void ResendEventFromAccount(Event evnt, List<Identifier>? targetsList, List<ValueTask<StatusReports>> tasks)
    {
        if(targetsList == null || !Identifiers.TryCreateRange(targetsList, out var to))
        {
            // No need to send to anyone
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        // Update the event
        evnt = evnt.WithOrigin(
            evnt.Origin with {
                From = Name.ToIdentifier(),
                To = to
            }
        );
        tasks.Add(Server.Post(evnt));
    }

    private async ValueTask HandleOutgoingSubscriptionRequest(Identifier source, Identifiers targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(Report(StatusCode.NotFound)));
                continue;
            }

            if(!TrySetPendingSubscriptionTo(targetAccount, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            var targetAccountIdentifier = targetAccount.ToIdentifier();

            if(updated.SubscriptionState.AcceptedTo)
            {
                // Already subscribed - confirm
                RouteToSessions(new SubscriptionAcceptedEvent {
                    Origin = EventOrigin.FromTo(targetAccountIdentifier, Name.ToIdentifier(), evnt.TransactionLanguage),
                    Processing = EventProcessing.NewInternal(),
                    Data = null
                }, source, tasks);
                continue;
            }

            if(!updated.SubscriptionState.PendingTo)
            {
                // Blocked for some reason
                tasks.Add(new(Report(StatusCode.NotAuthorized)));
                continue;
            }

            // Inform of subscribing to contact
            await ContactUpdate(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(targetAccountIdentifier);
        }

        ResendEventFromAccount(evnt, targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionRequest(Identifier identifier, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(Report(StatusCode.InvalidRequest)));
            return;
        }

        if(!TrySetPendingSubscriptionFrom(senderAccount, out _, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        if(updated.SubscriptionState.AcceptedFrom)
        {
            // Auto-accepted from approved state - update and reply back

            await ContactUpdate(updated, contacts, tasks);

            OnSubscribed(identifier, tasks);
            return;
        }

        if(!updated.SubscriptionState.PendingFrom)
        {
            // Blocked for some reason
            tasks.Add(new(Report(StatusCode.NotAuthorized)));
            return;
        }

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        // TODO Send unavailable? (Privacy)
    }

    private async ValueTask HandleOutgoingSubscriptionAcceptation(Identifier source, Identifiers targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(Report(StatusCode.NotFound)));
                continue;
            }

            // Update confirmation
            if(!TrySetAcceptedSubscriptionFrom(targetAccount, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            if(!updated.SubscriptionState.AcceptedFrom)
            {
                // Was not pending

                if(!updated.SubscriptionState.ApprovedFrom)
                {
                    // Blocked for some reason
                    tasks.Add(new(Report(StatusCode.NotAuthorized)));
                    continue;
                }

                // Only approved - update contact
                await ContactUpdate(updated, contacts, tasks);
                continue;
            }

            // Inform of updated contact
            await ContactUpdate(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(targetAccount.ToIdentifier());
        }

        ResendEventFromAccount(evnt, targetsList, tasks);
        OnSubscribed(targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionAcceptation(Identifier identifier, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(Report(StatusCode.InvalidRequest)));
            return;
        }

        if(!TrySetAcceptedSubscriptionTo(senderAccount, out _, out var updated, out var contacts) || !updated.SubscriptionState.AcceptedTo)
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        // Inform of updated contact
        await ContactUpdate(updated, contacts, tasks);

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }
    }

    private async ValueTask HandleOutgoingSubscriptionRejection(Identifier source, Identifiers targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? unavailableList = null;
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(Report(StatusCode.NotFound)));
                continue;
            }

            if(!TrySetCancelledSubscriptionFrom(targetAccount, out var previous, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            // Inform of updated contact
            await ContactUpdateOrRemove(previous, updated, contacts, tasks);

            var targetAccountIdentifier = targetAccount.ToIdentifier();

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
                    tasks.Add(new(Report(StatusCode.Success)));
                    continue;
            }

            // Pass the event through
            (targetsList ??= new()).Add(targetAccountIdentifier);
        }

        OnUnsubscribed(unavailableList, tasks);
        ResendEventFromAccount(evnt, targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionRejection(Identifier identifier, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(Report(StatusCode.InvalidRequest)));
            return;
        }

        if(!TrySetCancelledSubscriptionTo(senderAccount, out _, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        // Inform of updated contact (must be after)
        await ContactUpdate(updated, contacts, tasks);
    }

    private async ValueTask HandleOutgoingSubscriptionCancellation(Identifier source, Identifiers targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var identifier in targets)
        {
            if(identifier.Account is not { } targetAccount)
            {
                // Unrecognized identifier
                tasks.Add(new(Report(StatusCode.NotFound)));
                continue;
            }

            if(!TrySetCancelledSubscriptionTo(targetAccount, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            // Inform of unsubscribing from contact
            await ContactUpdate(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(targetAccount.ToIdentifier());
        }

        ResendEventFromAccount(evnt, targetsList, tasks);
    }

    private async ValueTask HandleIncomingSubscriptionCancellation(Identifier identifier, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(identifier.Account is not { } senderAccount)
        {
            // Unrecognized identifier
            tasks.Add(new(Report(StatusCode.InvalidRequest)));
            return;
        }

        if(!TrySetCancelledSubscriptionFrom(senderAccount, out var previous, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
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

        await ContactUpdateOrRemove(previous, updated, contacts, tasks);

        // Send as unavailable
        OnUnsubscribed(new(identifier), tasks);
    }

    private void OnSubscribed(Identifiers targets, List<ValueTask<StatusReports>> tasks)
    {
        // Prepare status event fields
        var origin = new EventOrigin() {
            From = default, // Filled later
            To = targets,
            TransactionIdentifier = null,
            TransactionLanguage = null
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
            tasks.Add(Server.Post(new StatusUpdateEvent {
                Origin = origin with {
                    From = session.Identifier
                },
                Processing = processing,
                Data = presence
            }));
        }
    }

    private void OnUnsubscribed(Identifiers targets, List<ValueTask<StatusReports>> tasks)
    {
        // Prepare unavailable event fields
        var origin = new EventOrigin() {
            From = default, // Filled later
            To = targets,
            TransactionIdentifier = null,
            TransactionLanguage = null
        };
        var processing = EventProcessing.NewInternal();
        var data = new PresenceData {
            Presentation = default,
            Priority = null,
            Status = new Status(Availability.Unavailable),
            Capabilities = null
        };
        foreach(var session in GetSessions(false))
        {
            if(session.Presence.Status.Availability == Availability.Unavailable)
            {
                // Was not announced
                continue;
            }

            // Send as unavailable
            tasks.Add(Server.Post(new StatusUpdateEvent {
                Origin = origin with {
                    From = session.Identifier
                },
                Processing = processing,
                Data = data
            }));
        }
    }

    private void OnSubscribed(IEnumerable<Identifier>? targetSequence, List<ValueTask<StatusReports>> tasks)
    {
        if(targetSequence == null || !Identifiers.TryCreateRange(targetSequence, out var targets))
        {
            return;
        }
        OnSubscribed(targets, tasks);
    }

    private void OnUnsubscribed(IEnumerable<Identifier>? targetSequence, List<ValueTask<StatusReports>> tasks)
    {
        if(targetSequence == null || !Identifiers.TryCreateRange(targetSequence, out var targets))
        {
            return;
        }
        OnUnsubscribed(targets, tasks);
    }
}
