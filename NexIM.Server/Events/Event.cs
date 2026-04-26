using System;
using System.Runtime.InteropServices;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Events;

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

    /// <inheritdoc cref="EventOrigin.From"/>
    public Identifier From => Origin.From;

    /// <inheritdoc cref="EventOrigin.To"/>
    public Identifiers To => Origin.To;

    /// <inheritdoc cref="EventOrigin.TransactionIdentifier"/>
    public Identifier? TransactionIdentifier => Origin.TransactionIdentifier;

    /// <inheritdoc cref="EventOrigin.TransactionLanguage"/>
    public LanguageCode? TransactionLanguage => Origin.TransactionLanguage;

    /// <inheritdoc cref="EventProcessing.Created"/>
    public DateTimeOffset Created => Processing.Created;

    /// <inheritdoc cref="EventProcessing.Published"/>
    public DateTimeOffset Published => Processing.Published;
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

/// <summary>
/// Stores the origin information of an event.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public record struct EventOrigin
{
    /// <summary>
    /// The identifier of the originating party.
    /// </summary>
    public required Identifier From { get; set; }

    /// <summary>
    /// The identifiers of the parties this event is being delivered to.
    /// </summary>
    public required Identifiers To { get; set; }

    /// <summary>
    /// The identifier of the event within the scope of the session between the two parties.
    /// </summary>
    public required Identifier? TransactionIdentifier { get; set; }

    /// <summary>
    /// The language that was used when constructing the event.
    /// </summary>
    public required LanguageCode? TransactionLanguage { get; set; }

    /// <summary>
    /// Retrieves a new <see cref="EventOrigin"/> instance responding to the event.
    /// </summary>
    public EventOrigin RespondFrom(Identifier from, LanguageCode? language = null)
    {
        return this with { From = from, To = From, TransactionLanguage = language };
    }

    /// <summary>
    /// Creates an <see cref="EventOrigin"/> instance
    /// between two parties.
    /// </summary>
    public static EventOrigin FromTo(Identifier from, Identifiers to, LanguageCode? transactionLanguage = null)
    {
        return new() {
            From = from,
            To = to,
            TransactionIdentifier = null,
            TransactionLanguage = transactionLanguage
        };
    }
}

/// <summary>
/// Stores the processing information of an event.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public record struct EventProcessing
{
    /// <summary>
    /// The earliest date and time this event was acknowledged.
    /// </summary>
    /// <remarks>
    /// Must not be greater than <see cref="Published"/>.
    /// </remarks>
    public required DateTimeOffset Created { get; set; }

    /// <summary>
    /// The date and time this event was fully accepted by the server.
    /// </summary>
    /// <remarks>
    /// Must not be less than <see cref="Created"/>.
    /// </remarks>
    public required DateTimeOffset Published { get; set; }

    /// <summary>
    /// Creates an <see cref="EventProcessing"/> instance for
    /// a newly produced event.
    /// </summary>
    public static EventProcessing Create()
    {
        var date = IdentifierHelper.IdentifierTimeNow;
        return new() {
            Created = date,
            Published = date
        };
    }

    /// <summary>
    /// Creates an <see cref="EventProcessing"/> instance for
    /// a finished event.
    /// </summary>
    public static EventProcessing Finish(DateTimeOffset created)
    {
        var date = IdentifierHelper.IdentifierTimeNow;
        if(created > date)
        {
            // Must be kept ordered
            created = date;
        }
        return new() {
            Created = created,
            Published = date
        };
    }
}
