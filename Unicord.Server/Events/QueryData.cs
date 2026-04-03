namespace Unicord.Server.Events;

/// <summary>
/// Stores data for a query event.
/// </summary>
public abstract record QueryData : EventData;

/// <summary>
/// Stores no additional query data other than extensions.
/// </summary>
public sealed record GeneralQueryData : QueryData;
