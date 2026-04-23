namespace NexIM.Server.Events;

/// <summary>
/// Represents a general query event.
/// </summary>
public abstract record QueryEvent : Event<QueryData>;

/// <summary>
/// Represents a request event. Such an event expects a response
/// or an error event as a reply.
/// </summary>
public abstract record RequestEvent : QueryEvent;

/// <summary>
/// Represents an information retrieval event.
/// </summary>
public sealed record RetrieveEvent : RequestEvent;

/// <summary>
/// Represents an information update event.
/// </summary>
public sealed record UpdateEvent : RequestEvent;

/// <summary>
/// Represents a response event to a request.
/// </summary>
public sealed record ResponseEvent : QueryEvent;
