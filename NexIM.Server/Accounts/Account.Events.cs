using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using NexIM.Server.Events;
using NexIM.Server.Tools;

namespace NexIM.Server.Accounts;

partial class Account : IEventHandler
{
    readonly Func<Identifier, TargetType> router;
    readonly Func<TargetType, Identifiers, MessageEvent, ValueTask<StatusReports>> messageTarget;
    readonly Func<TargetType, Identifiers, PresenceEvent, ValueTask<StatusReports>> presenceTarget;
    readonly Func<TargetType, Identifiers, Event, ValueTask<StatusReports>> generalTarget;

    private ValueTuple Events {
        [MemberNotNull(nameof(messageTarget))]
        [MemberNotNull(nameof(presenceTarget))]
        [MemberNotNull(nameof(generalTarget))]
        [MemberNotNull(nameof(router))]
        init {
            messageTarget = RouteMessage;
            presenceTarget = RoutePresence;
            generalTarget = RouteEvent;
            router =
                identifier =>
                    identifier.Account == Name
                    ? identifier.Resource != null
                    ? TargetType.Sessions
                    : TargetType.Account
                    : TargetType.Server;
        }
    }

    public ValueTask<StatusReports> Post(Event evnt)
    {
        switch(evnt)
        {
            case MessageEvent msgEvent:
                // TODO Message duplicating logic (carbons)
                return msgEvent.To.Route(router, messageTarget, msgEvent);
            case PresenceEvent presEvent:
                return presEvent.To.Route(router, presenceTarget, presEvent);
            default:
                return evnt.To.Route(router, generalTarget, evnt);
        }
    }

    private StatusReport Report(StatusCode code)
    {
        return new(Name.ToIdentifier(), code);
    }

    private ValueTask<StatusReports> RouteMessage(TargetType targetType, Identifiers targetTo, MessageEvent msgEvent)
    {
        List<ValueTask<StatusReports>> tasks;
        switch(targetType)
        {
            case TargetType.Server:
                // Not intended for this account
                return Server.Post(msgEvent.WithTo(targetTo));

            case TargetType.Sessions:
                tasks = new();
                RouteToSessions(msgEvent, targetTo, tasks);
                break;

            default:
                // Deliver to all sessions with top priority
                // TODO Different receiving strategy
                tasks = new();
                sbyte? priority = null;
                foreach(var session in GetSessions(true))
                {
                    if(priority is { } previousPriority && session.Priority < previousPriority)
                    {
                        // Less priority from now on
                        break;
                    }
                    priority = session.Priority;
                    tasks.Add(session.Outbound(msgEvent));
                }
                break;
        }

        return tasks.Combine();
    }

    internal bool CanSharePresenceWith(Identifier identifier)
    {
        if(identifier.Account is not { } account)
        {
            return false;
        }
        return account == Name || GetContact(account)?.SubscriptionState.AcceptedFrom == true;
    }

    private ValueTask<StatusReports> RoutePresence(TargetType targetType, Identifiers targetTo, PresenceEvent presEvent)
    {
        List<ValueTask<StatusReports>> tasks;
        switch(targetType)
        {
            case TargetType.Server:
                // Presence routed to the server must come from within the account
                Debug.Assert(Name == presEvent.From.Account);
                switch(presEvent)
                {
                    case SubscriptionEvent subEvent:
                        return OnOutgoingSubscriptionEvent(subEvent);
                    case StatusRequestEvent:
                    // TODO: Cache contact's presence to avoid sending this
                    default:
                        // Route normally
                        return Server.Post(presEvent);
                }

            case TargetType.Sessions:
                switch(presEvent)
                {
                    case SubscriptionEvent subEvent:
                        // Handle on behalf of the account anyway
                        return OnIncomingSubscriptionEvent(subEvent.WithTo(Name.ToIdentifier()));
                }

                tasks = new();
                RouteToSessions(presEvent, targetTo, tasks);
                return tasks.Combine();

            default:
                switch(presEvent)
                {
                    case SubscriptionEvent subEvent:
                        return OnIncomingSubscriptionEvent(subEvent);
                }

                // Deliver to all sessions
                tasks = new();
                foreach(var session in GetSessions(false))
                {
                    // Also includes the originating session (echo)
                    tasks.Add(session.Outbound(presEvent));
                }

                if(presEvent.From.Account == Name)
                {
                    // Deliver to all eligible contacts
                    var subscribedSet = Identifiers.Builder.Empty;
                    foreach(var contact in Contacts)
                    {
                        if(!contact.SubscriptionState.AcceptedFrom)
                        {
                            // Not accepted
                            continue;
                        }

                        subscribedSet.Add(contact.Account.ToIdentifier());
                    }

                    if(subscribedSet.TryToSet() is { } contactIdentifiers)
                    {
                        // Send
                        tasks.Add(Server.Post(presEvent.WithTo(contactIdentifiers)));
                    }
                }

                return tasks.Combine();
        }
    }

    private ValueTask<StatusReports> RouteEvent(TargetType targetType, Identifiers targetTo, Event evnt)
    {
        switch(targetType)
        {
            case TargetType.Server:
                return Server.Post(evnt);
            case TargetType.Sessions:
                var tasks = new List<ValueTask<StatusReports>>();
                RouteToSessions(evnt, targetTo, tasks);
                return tasks.Combine();
            default:
                return OnQuery(evnt);
        }
    }

    private ValueTask<StatusReports>? OnOwnerQuery(Event evnt)
    {
        switch(evnt)
        {
            case RetrieveEvent { Data: RosterQueryData data }:
                // Retrieving the roster
                var roster = contacts.Values;
                if(data.Roster == roster || UInt32.TryParse(data.Tag, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tag) && tag == GetRosterVersion(roster))
                {
                    // Same version
                    // TODO Incremental changes
                    return Post(new ResponseEvent {
                        Origin = evnt.Origin.RespondFrom(Name.ToIdentifier()),
                        Processing = EventProcessing.Create(),
                        Data = null
                    });
                }
                return Post(new ResponseEvent {
                    Origin = evnt.Origin.RespondFrom(Name.ToIdentifier()),
                    Processing = EventProcessing.Create(),
                    Data = new RosterQueryData {
                        Tag = GetRosterVersionString(roster),
                        Roster = roster
                    }
                });

            case UpdateEvent { Data: RosterUpdateData data }:
                // Updating a contact
                return UpdateContact(data.Contact);

            case UpdateEvent { Data: RosterRemoveData data }:
                // Removing a contact
                return RemoveContact(data.Contact.Account);

            case RetrieveEvent { Data: PrivateData data }:
                // Retrieving private data
                if(!privateStorage.TryGetValue(data.Key, out var storedData))
                {
                    // Not present
                    return new(Report(StatusCode.NotFound));
                }
                return Post(new ResponseEvent {
                    Origin = evnt.Origin.RespondFrom(Name.ToIdentifier(), storedData.Language),
                    Processing = EventProcessing.Create(),
                    Data = storedData.EventData
                });

            case UpdateEvent { Data: PrivateData data }:
                // Updating private data
                privateStorage.SetItem(data.Key, PrivateStorageData.Create(this, data, evnt.Origin.TransactionLanguage));
                return Save();
            
            case UpdateEvent { Data: VCardQueryData vcardData }:
                // Updating vCard
                VCard = vcardData.VCard;
                return Save();

            default:
                return null;
        }
    }

    private ValueTask<StatusReports> OnQuery(Event evnt)
    {
        if(evnt.From.Account == Name)
        {
            // Event comes from owner
            if(OnOwnerQuery(evnt) is { } task)
            {
                // Recognized
                return task;
            }
        }

        switch(evnt)
        {
            case RetrieveEvent { Data: VCardQueryData }:
                // Retrieving a VCard
                var vcard = VCard;
                if(vcard == null)
                {
                    return new(Report(StatusCode.NotFound));
                }
                if(vcard.PrivacyClassification is VCards.VCardPrivacyClassification.Private or VCards.VCardPrivacyClassification.Confidential)
                {
                    // TODO Figure out what "confidential" means
                    if(evnt.From.Account != Name)
                    {
                        // Only the owner can view
                        return new(Report(StatusCode.Unauthorized));
                    }
                }
                return Post(new ResponseEvent {
                    Origin = evnt.Origin.RespondFrom(Name.ToIdentifier()),
                    Processing = EventProcessing.Create(),
                    Data = new VCardQueryData {
                        VCard = vcard
                    }
                });

            case QueryEvent { Data: RosterQueryData or PrivateData or VCardQueryData }:
                // Supported but must be owner
                return new(Report(StatusCode.Unauthorized));

            default:
                // Can't process arbitrary event
                return new(Report(StatusCode.UnrecognizedRequest));
        }
    }

    private void RouteToSessions(Event evnt, Identifiers sessions, List<ValueTask<StatusReports>> tasks)
    {
        foreach(var identifier in sessions)
        {
            if(identifier.Resource is not { } resource || identifier.Account != Name)
            {
                throw new ArgumentException("Argument must identify the account's sessions.", nameof(sessions));
            }
            // Local delivery - pick individual session
            if(GetSession(resource) is not { } session)
            {
                tasks.Add(new(Report(StatusCode.NotFound)));
                continue;
            }
            tasks.Add(session.Outbound(evnt.WithTo(identifier)));
        }
    }

    private void RouteToAllSessions(Event evnt, List<ValueTask<StatusReports>> tasks)
    {
        foreach(var session in GetSessions(false))
        {
            tasks.Add(session.Outbound(evnt.WithTo(session.Identifier)));
        }
    }

    private ValueTask<StatusReports> OnOutgoingSubscriptionEvent(SubscriptionEvent presEvent)
    {
        var tasks = new List<ValueTask<StatusReports>>();

        var from = presEvent.From;
        switch(presEvent)
        {
            case SubscriptionRequestedEvent:
                HandleOutgoingSubscriptionRequest(from, presEvent.To, presEvent, tasks);
                break;
            case SubscriptionAcceptedEvent:
                HandleOutgoingSubscriptionAcceptation(from, presEvent.To, presEvent, tasks);
                break;
            case SubscriptionRejectedEvent:
                HandleOutgoingSubscriptionRejection(from, presEvent.To, presEvent, tasks);
                break;
            case SubscriptionCancelledEvent:
                HandleOutgoingSubscriptionCancellation(from, presEvent.To, presEvent, tasks);
                break;
        }

        return tasks.Combine();
    }

    private ValueTask<StatusReports> OnIncomingSubscriptionEvent(SubscriptionEvent presEvent)
    {
        var tasks = new List<ValueTask<StatusReports>>();

        var from = presEvent.From;
        switch(presEvent)
        {
            case SubscriptionRequestedEvent:
                HandleIncomingSubscriptionRequest(from, presEvent, tasks);
                break;
            case SubscriptionAcceptedEvent:
                HandleIncomingSubscriptionAcceptation(from, presEvent, tasks);
                break;
            case SubscriptionRejectedEvent:
                HandleIncomingSubscriptionRejection(from, presEvent, tasks);
                break;
            case SubscriptionCancelledEvent:
                HandleIncomingSubscriptionCancellation(from, presEvent, tasks);
                break;
        }

        return tasks.Combine();
    }

    enum TargetType
    {
        Server,
        Account,
        Sessions
    }
}
