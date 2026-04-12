using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account : IEventHandler
{
    readonly Func<Identifier, TargetType> router;
    readonly Func<TargetType, Identifiers, MessageEvent, ValueTask<ErrorCode>> messageTarget;
    readonly Func<TargetType, Identifiers, PresenceEvent, ValueTask<ErrorCode>> presenceTarget;
    readonly Func<TargetType, Identifiers, Event, ValueTask<ErrorCode>> generalTarget;

    private void InitEvents(out Func<Identifier, TargetType> router, out Func<TargetType, Identifiers, MessageEvent, ValueTask<ErrorCode>> messageTarget, out Func<TargetType, Identifiers, PresenceEvent, ValueTask<ErrorCode>> presenceTarget, out Func<TargetType, Identifiers, Event, ValueTask<ErrorCode>> generalTarget)
    {
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

    public ValueTask<ErrorCode> Post(Event evnt)
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

    private ValueTask<ErrorCode> RouteMessage(TargetType targetType, Identifiers targetTo, MessageEvent msgEvent)
    {
        if(targetType == TargetType.Server)
        {
            // Not intended for this account
            return Server.Post(msgEvent.WithTo(targetTo));
        }

        var tasks = new List<ValueTask<ErrorCode>>();

        switch(targetType)
        {
            case TargetType.Sessions:
                RouteToSessions(msgEvent, targetTo, tasks);
                break;
            case TargetType.Account:
                // Deliver to all sessions with top priority
                // TODO Different receiving strategy
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
        return account == Name || GetContact(account)?.SubscriptionState.From == SubscriptionLevel.Accepted;
    }

    private ValueTask<ErrorCode> RoutePresence(TargetType targetType, Identifiers targetTo, PresenceEvent presEvent)
    {
        List<ValueTask<ErrorCode>> tasks;
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
                        // Must not target individiual sessions
                        return new(ErrorCode.InvalidRequest);
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
                    tasks.Add(session.Outbound(presEvent));
                }
                return tasks.Combine();
        }
    }

    private ValueTask<ErrorCode> RouteEvent(TargetType targetType, Identifiers targetTo, Event evnt)
    {
        switch(targetType)
        {
            case TargetType.Server:
                return Server.Post(evnt);
            case TargetType.Sessions:
                var tasks = new List<ValueTask<ErrorCode>>();
                RouteToSessions(evnt, targetTo, tasks);
                return tasks.Combine();
            default:
                return OnQuery(evnt);
        }
    }

    private async ValueTask<ErrorCode> OnQuery(Event evnt)
    {
        switch(evnt)
        {
            case RetrieveEvent { Data: VCardQueryData }:
                // Retrieving a VCard
                var vcard = VCard;
                if(vcard == null)
                {
                    return ErrorCode.NotAvailable;
                }
                if(vcard.PrivacyClassification is VCards.VCardPrivacyClassification.Private or VCards.VCardPrivacyClassification.Confidential)
                {
                    // TODO Figure out what "confidential" means
                    return ErrorCode.NotAuthorized;
                }
                return await Post(new ResponseEvent {
                    Origin = evnt.Origin with {
                        From = new Identifier(Name, null),
                        To = evnt.From
                    },
                    Processing = EventProcessing.NewInternal(),
                    Data = new VCardQueryData {
                        VCard = vcard
                    }
                });;
            case UpdateEvent { Data: VCardQueryData vcardData }:
                if(evnt.From.Account != Name)
                {
                    // The owner can modify
                    return ErrorCode.NotAuthorized;
                }
                VCard = vcardData.VCard;
                return ErrorCode.Success;
        }
        // Can't process arbitrary event
        return ErrorCode.Unrecognized;
    }

    private void RouteToSessions(Event evnt, Identifiers sessions, List<ValueTask<ErrorCode>> tasks)
    {
        foreach(var identifier in sessions)
        {
            if(identifier.Resource is not { } resource)
            {
                tasks.Add(new(ErrorCode.NotFound));
                continue;
            }
            // Local delivery - pick individual session
            if(GetSession(resource) is not { } session)
            {
                tasks.Add(new(ErrorCode.NotFound));
                continue;
            }
            tasks.Add(session.Outbound(evnt.WithTo(identifier)));
        }
    }

    private async ValueTask<ErrorCode> OnOutgoingSubscriptionEvent(SubscriptionEvent presEvent)
    {
        var tasks = new List<ValueTask<ErrorCode>>();

        var from = presEvent.From;
        switch(presEvent)
        {
            case SubscriptionRequestedEvent:
                await HandleOutgoingSubscriptionRequest(from, presEvent.To, presEvent, tasks);
                break;
            case SubscriptionAcceptedEvent:
                await HandleOutgoingSubscriptionAcceptation(from, presEvent.To, presEvent, tasks);
                break;
            case SubscriptionRejectedEvent:
                await HandleOutgoingSubscriptionRejection(from, presEvent.To, presEvent, tasks);
                break;
            case SubscriptionCancelledEvent:
                await HandleOutgoingSubscriptionCancellation(from, presEvent.To, presEvent, tasks);
                break;
        }

        return await tasks.Combine();
    }

    private async ValueTask<ErrorCode> OnIncomingSubscriptionEvent(SubscriptionEvent presEvent)
    {
        var tasks = new List<ValueTask<ErrorCode>>();

        var from = presEvent.From;
        switch(presEvent)
        {
            case SubscriptionRequestedEvent:
                await HandleIncomingSubscriptionRequest(from, presEvent, tasks);
                break;
            case SubscriptionAcceptedEvent:
                await HandleIncomingSubscriptionAcceptation(from, presEvent, tasks);
                break;
            case SubscriptionRejectedEvent:
                await HandleIncomingSubscriptionRejection(from, presEvent, tasks);
                break;
            case SubscriptionCancelledEvent:
                await HandleIncomingSubscriptionCancellation(from, presEvent, tasks);
                break;
        }

        return await tasks.Combine();
    }

    enum TargetType
    {
        Server,
        Account,
        Sessions
    }
}
