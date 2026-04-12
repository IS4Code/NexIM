using Unicord.Server.Accounts.VCards;

namespace Unicord.Server.Events;

/// <summary>
/// Stores data for a query event.
/// </summary>
public abstract record QueryData : EventData;

/// <summary>
/// Stores no additional query data other than extensions.
/// </summary>
public sealed record GeneralQueryData : QueryData;

public sealed record VCardQueryData : QueryData
{
    public required VCard? VCard { get; init; }
}
