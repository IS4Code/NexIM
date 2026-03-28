namespace Unicord.Server.Events;

/// <summary>
/// Represents a presence event. Such an event contains availability
/// information about the source and is suitable for broadcast.
/// </summary>
public abstract record PresenceEvent : Event<PresenceData>;

public abstract record StatusEvent : PresenceEvent;
public sealed record StatusUpdateEvent : StatusEvent;
public sealed record StatusRequestEvent : StatusEvent;

public abstract record SubscriptionEvent : PresenceEvent;
public sealed record SubscriptionRequestedEvent : SubscriptionEvent;
public sealed record SubscriptionAcceptedEvent : SubscriptionEvent;
public sealed record SubscriptionRejectedEvent : SubscriptionEvent;
public sealed record SubscriptionCancelledEvent : SubscriptionEvent;
