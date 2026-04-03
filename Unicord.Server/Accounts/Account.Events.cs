using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account : IEventHandler
{
    readonly Func<Identifier, EnumWrapper<TargetType>> router;
    readonly Func<EnumWrapper<TargetType>, IdentifierSet, MessageEvent, ValueTask<ErrorCode>> messageTarget;
    readonly Func<EnumWrapper<TargetType>, IdentifierSet, PresenceEvent, ValueTask<ErrorCode>> presenceTarget;
    readonly Func<EnumWrapper<TargetType>, IdentifierSet, Event, ValueTask<ErrorCode>> generalTarget;

    public ValueTask<ErrorCode> Post(Event evnt)
    {
        switch(evnt)
        {
            case MessageEvent msgEvent:
                return PostMessage(msgEvent);
            case PresenceEvent presEvent:
                return PostPresence(presEvent);
            default:
                if(evnt.To.IsEmpty)
                {
                    // No recipient
                    return new(ErrorCode.InvalidRequest);
                }
                return evnt.To.Route(router, generalTarget, evnt);
        }
    }

    private ValueTask<ErrorCode> PostMessage(MessageEvent msgEvent)
    {
        // TODO Message duplicating logic (carbons)

        if(msgEvent.To.IsEmpty)
        {
            // Target the account
            return RouteMessage(TargetType.Account, default, msgEvent);
        }

        return msgEvent.To.Route(router, messageTarget, msgEvent);
    }

    private ValueTask<ErrorCode> PostPresence(PresenceEvent presEvent)
    {
        if(presEvent.To.IsEmpty)
        {
            if(presEvent is not StatusEvent)
            {
                // Broadcasting is not permitted
                return new(ErrorCode.InvalidRequest);
            }

            // Select all contacts and other connected resources
            var to = new IdentifierSet(
                GetSessions(false).Select(session => new Identifier(Name, session.Resource))
                .Concat(Contacts.Select(contact => new Identifier(contact.Account, null)))
            ).Remove(presEvent.From);

            if(to.IsEmpty)
            {
                // Nobody to send to
                return new(ErrorCode.Success);
            }

            presEvent = presEvent.WithTo(to);
        }

        if(presEvent is SubscriptionEvent subscriptionEvent)
        {
            // Custom routing
            return OnSubscriptionEvent(subscriptionEvent);
        }

        return presEvent.To.Route(router, presenceTarget, presEvent);
    }

    private void InitEvents(out Func<Identifier, EnumWrapper<TargetType>> router, out Func<EnumWrapper<TargetType>, IdentifierSet, MessageEvent, ValueTask<ErrorCode>> messageTarget, out Func<EnumWrapper<TargetType>, IdentifierSet, PresenceEvent, ValueTask<ErrorCode>> presenceTarget, out Func<EnumWrapper<TargetType>, IdentifierSet, Event, ValueTask<ErrorCode>> generalTarget)
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

    private ValueTask<ErrorCode> RouteMessage(EnumWrapper<TargetType> targetType, IdentifierSet targetTo, MessageEvent msgEvent)
    {
        if(targetType.Value == TargetType.Server)
        {
            // Not intended for this account
            return Server.Post(msgEvent.WithTo(targetTo));
        }

        var tasks = new List<ValueTask<ErrorCode>>();

        switch(targetType.Value)
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

    private ValueTask<ErrorCode> RoutePresence(EnumWrapper<TargetType> targetType, IdentifierSet targetTo, PresenceEvent presEvent)
    {
        if(targetType.Value == TargetType.Server)
        {
            // Not intended for this account
            return Server.Post(presEvent);
        }

        var tasks = new List<ValueTask<ErrorCode>>();
        
        switch(targetType.Value)
        {
            case TargetType.Sessions:
                RouteToSessions(presEvent, targetTo, tasks);
                break;
            case TargetType.Account:
                // Deliver to all sessions
                foreach(var session in GetSessions(false))
                {
                    tasks.Add(session.Outbound(presEvent));
                }
                break;
        }

        return tasks.Combine();
    }

    private ValueTask<ErrorCode> RouteEvent(EnumWrapper<TargetType> targetType, IdentifierSet targetTo, Event evnt)
    {
        switch(targetType.Value)
        {
            case TargetType.Server:
                return Server.Post(evnt);
            case TargetType.Sessions:
                var tasks = new List<ValueTask<ErrorCode>>();
                RouteToSessions(evnt, targetTo, tasks);
                return tasks.Combine();
            default:
                // Can't process arbitrary event
                return new(ErrorCode.Unrecognized);
        }
    }

    private void RouteToSessions(Event evnt, IdentifierSet sessions, List<ValueTask<ErrorCode>> tasks)
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
            tasks.Add(session.Outbound(evnt.WithTo(new(identifier))));
        }
    }

    private async ValueTask<ErrorCode> OnSubscriptionEvent(PresenceEvent presEvent)
    {
        var tasks = new List<ValueTask<ErrorCode>>();

        var from = presEvent.From;
        if(from.Account == Name)
        {
            // Outgoing request
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
        }
        else
        {
            // Incoming request
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
        }

        return await tasks.Combine();
    }

    enum TargetType
    {
        Server,
        Account,
        Sessions
    }

    [StructLayout(LayoutKind.Auto)]
    readonly record struct EnumWrapper<TEnum>(TEnum Value) where TEnum : struct, Enum
    {
        public static implicit operator EnumWrapper<TEnum>(TEnum value) => new(value);
        public static implicit operator TEnum(EnumWrapper<TEnum> wrapper) => wrapper.Value;
    }
}
