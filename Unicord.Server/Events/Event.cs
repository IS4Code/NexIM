using System;
using Unicord.Server.Accounts;

namespace Unicord.Server.Events;

/// <summary>
/// Represents an event originating from a party.
/// </summary>
public abstract record Event
{
    /// <summary>
    /// Information about the source of the event.
    /// </summary>
    public required EventOrigin Origin { get; init; }

    /// <summary>
    /// Information about the processing of the event.
    /// </summary>
    public required EventProcessing Processing { get; set; }

    /// <summary>
    /// The identifier of the originating party.
    /// </summary>
    public Identifier From => Origin.From;

    /// <summary>
    /// The identifiers of the parties this event is being delivered to.
    /// </summary>
    public IdentifierSet To => Origin.To;

    /// <summary>
    /// The identifier of the event within the scope of the session between the two parties.
    /// </summary>
    public Identifier? TransactionIdentifier => Origin.TransactionIdentifier;

    /// <summary>
    /// The date and time this event was received by the server.
    /// </summary>
    public DateTimeOffset Received => Processing.Received;

    /// <summary>
    /// The date and time this event's main content was received by the server.
    /// </summary>
    public DateTimeOffset? Accepted => Processing.Accepted;

    /// <summary>
    /// The date and time this event was fully processed by the server.
    /// </summary>
    public DateTimeOffset? Published => Processing.Published;

    /// <summary>
    /// The canonical date and time of this event.
    /// </summary>
    public DateTimeOffset Created => Processing.Created;
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
    public required TData? Data { get; init; }
}

/// <summary>
/// Stores the origin information of an event.
/// </summary>
public record struct EventOrigin
{
    /// <summary>
    /// The identifier of the originating party.
    /// </summary>
    public required Identifier From { get; set; }

    /// <summary>
    /// The identifiers of the parties this event is being delivered to.
    /// </summary>
    public required IdentifierSet To { get; set; }

    /// <summary>
    /// The identifier of the event within the scope of the session between the two parties.
    /// </summary>
    public required Identifier? TransactionIdentifier { get; set; }
}

/// <summary>
/// Stores the processing information of an event.
/// </summary>
public record struct EventProcessing
{
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

    /// <summary>
    /// Creates a new artificial <see cref="EventProcessing"/> instance for
    /// a newly produced event.
    /// </summary>
    public static EventProcessing NewInternal()
    {
        var date = DateTime.UtcNow;
        return new()
        {
            Received = date,
            Accepted = date,
            Published = date
        };
    }
}
