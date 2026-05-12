using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using NexIM.Server.Events;

namespace NexIM.Server.Accounts;

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

    private void HandleOutgoingSubscriptionRequest(Identifier source, Identities targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var id in targets)
        {
            if(!TrySetPendingSubscriptionTo(id, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            var target = id.Name.ToIdentifier();

            if(updated.SubscriptionState.AcceptedTo)
            {
                // Already subscribed - confirm
                RouteToSessions(new SubscriptionAcceptedEvent {
                    Origin = EventOrigin.FromTo(target, Name.ToIdentifier(), evnt.TransactionLanguage),
                    Processing = EventProcessing.Create(),
                    Data = PresenceData.Empty
                }, source, tasks, false);
                continue;
            }

            if(!updated.SubscriptionState.PendingTo)
            {
                // Blocked for some reason
                tasks.Add(new(Report(StatusCode.Blocked)));
                continue;
            }

            // Inform of subscribing to contact
            OnContactUpdated(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(target);
        }

        tasks.Add(Save());

        ResendEventFromAccount(evnt, targetsList, tasks);
    }

    private void HandleIncomingSubscriptionRequest(Identity id, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(!TrySetPendingSubscriptionFrom(id, out _, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        tasks.Add(Save());

        if(updated.SubscriptionState.AcceptedFrom)
        {
            // Auto-accepted from approved state - update and reply back

            OnContactUpdated(updated, contacts, tasks);

            OnSubscribed(id.Name.ToIdentifier(), tasks);
            return;
        }

        if(!updated.SubscriptionState.PendingFrom)
        {
            // Blocked for some reason
            tasks.Add(new(Report(StatusCode.Blocked)));
            return;
        }

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        // TODO Send unavailable? (Privacy)
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Consistency")]
    private void HandleOutgoingSubscriptionAcceptation(Identifier source, Identities targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var id in targets)
        {
            // Update confirmation
            if(!TrySetAcceptedSubscriptionFrom(id, out _, out var updated, out var contacts))
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
                    tasks.Add(new(Report(StatusCode.Blocked)));
                    continue;
                }

                // Only approved - update contact
                OnContactUpdated(updated, contacts, tasks);
                continue;
            }

            // Inform of updated contact
            OnContactUpdated(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(id.Name.ToIdentifier());
        }

        tasks.Add(Save());

        ResendEventFromAccount(evnt, targetsList, tasks);
        OnSubscribed(targetsList, tasks);
    }

    private void HandleIncomingSubscriptionAcceptation(Identity id, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(!TrySetAcceptedSubscriptionTo(id, out _, out var updated, out var contacts) || !updated.SubscriptionState.AcceptedTo)
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        tasks.Add(Save());

        // Inform of updated contact
        OnContactUpdated(updated, contacts, tasks);

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Consistency")]
    private void HandleOutgoingSubscriptionRejection(Identifier source, Identities targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? unavailableList = null;
        List<Identifier>? targetsList = null;

        foreach(var id in targets)
        {
            if(!TrySetCancelledSubscriptionFrom(id, out var previous, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            // Inform of updated contact
            OnContactUpdateOrRemoved(previous, updated, contacts, tasks);

            var target = id.Name.ToIdentifier();

            switch(previous.SubscriptionState)
            {
                case { AcceptedFrom: true }:
                    // Stopped allowing subscription
                    (unavailableList ??= new()).Add(target);
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
            (targetsList ??= new()).Add(target);
        }

        tasks.Add(Save());

        OnUnsubscribed(unavailableList, tasks);
        ResendEventFromAccount(evnt, targetsList, tasks);
    }

    private void HandleIncomingSubscriptionRejection(Identity id, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(!TrySetCancelledSubscriptionTo(id, out _, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        tasks.Add(Save());

        // Route to sessions
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt));
        }

        // Inform of updated contact (must be after)
        OnContactUpdated(updated, contacts, tasks);
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Consistency")]
    private void HandleOutgoingSubscriptionCancellation(Identifier source, Identities targets, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        List<Identifier>? targetsList = null;

        foreach(var id in targets)
        {
            if(!TrySetCancelledSubscriptionTo(id, out _, out var updated, out var contacts))
            {
                // No change
                tasks.Add(new(Report(StatusCode.Success)));
                continue;
            }

            // Inform of unsubscribing from contact
            OnContactUpdated(updated, contacts, tasks);

            // Pass the event through
            (targetsList ??= new()).Add(id.Name.ToIdentifier());
        }

        tasks.Add(Save());

        ResendEventFromAccount(evnt, targetsList, tasks);
    }

    private void HandleIncomingSubscriptionCancellation(Identity id, Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        if(!TrySetCancelledSubscriptionFrom(id, out var previous, out var updated, out var contacts))
        {
            // No change
            tasks.Add(new(Report(StatusCode.Success)));
            return;
        }

        tasks.Add(Save());

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

        OnContactUpdateOrRemoved(previous, updated, contacts, tasks);

        // Send as unavailable
        OnUnsubscribed(id.Name.ToIdentifier(), tasks);
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
        var processing = EventProcessing.Create();
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
        var processing = EventProcessing.Create();
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
                Data = PresenceData.Empty
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
