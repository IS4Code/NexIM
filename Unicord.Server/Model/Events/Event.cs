using System;

namespace Unicord.Server.Model.Events;

/// <summary>
/// Represents an event originating from a party.
/// </summary>
public abstract record Event
{
    /// <summary>
    /// The identifier of the originating party.
    /// </summary>
    public required Identifier? From { get; init; }

    /// <summary>
    /// The identifier of the party this event is being delivered to.
    /// </summary>
    public required Identifier? To { get; init; }

    /// <summary>
    /// The identifier of the event within the scope of the session between the two parties.
    /// </summary>
    public required Identifier? TransactionIdentifier { get; init; }

    /// <summary>
    /// The date and time this event was received by the server.
    /// </summary>
    /// <remarks>
    /// Must not be greater than <see cref="Accepted"/> or <see cref="Published"/>.
    /// </remarks>
    public required DateTimeOffset Received { get; set; }

    /// <summary>
    /// The date and time this event's main content was received by the server.
    /// </summary>
    /// <remarks>
    /// Must not be greater than <see cref="Published"/> or less than <see cref="Received"/>.
    /// </remarks>
    public required DateTimeOffset? Accepted { get; set; }

    /// <summary>
    /// The date and time this event was fully processed by the server.
    /// </summary>
    /// <remarks>
    /// Must not be less than <see cref="Received"/> or <see cref="Accepted"/>.
    /// </remarks>
    public required DateTimeOffset? Published { get; set; }

    /// <summary>
    /// The canonical date and time of this event.
    /// </summary>
    /// <remarks>
    /// This is the first value in the sequence <see cref="Accepted"/>,
    /// <see cref="Published"/>, <see cref="Received"/> that is set.
    /// </remarks>
    public DateTimeOffset Created => Accepted ?? Published ?? Received;
}

/// <summary>
/// Represents an event originating from a party with a particular payload type.
/// </summary>
/// <typeparam name="TData">The type of the payload.</typeparam>
public abstract record Event<TData> : Event where TData : EventData
{
    /// <summary>
    /// The data associated with the event, indicating the type and content of the event.
    /// </summary>
    public required TData Data { get; init; }
}
