using System;
using System.Collections.Generic;
using System.Xml.Linq;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;

namespace NexIM.Server.Events;

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

public record RosterQueryData : QueryData
{
    public required IReadOnlyCollection<Contact>? Roster { get; init; }
    public required string? Tag { get; init; }
}

public sealed record RosterUpdateData : RosterQueryData
{
    public required Contact Contact { get; init; }
}

public sealed record RosterRemoveData : RosterQueryData
{
    public required Contact Contact { get; init; }
}

public sealed record PrivateData : QueryData
{
    public required XName Key { get; init; }
}

public sealed record TimeData : QueryData
{
    public required DateTimeOffset? DateTime { get; init; }
}

public sealed record PingData : QueryData
{
    public static readonly PingData Empty = new();
}
