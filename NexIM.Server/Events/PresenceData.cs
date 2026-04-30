using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Events;

/// <summary>
/// Stores data for a presence.
/// </summary>
public sealed record PresenceData : DeliveryData
{
    public static readonly PresenceData Empty = new() {
        Presentation = default,
        Status = default,
        Priority = null,
        Capabilities = default,
        DelayedBy = null,
        DelayReason = null,
        Addresses = null,
        ReceiptIdentifier = null
    };

    /// <summary>
    /// The sender's presentation.
    /// </summary>
    public required SenderPresentation Presentation { get; init; }

    /// <summary>
    /// The sender's status.
    /// </summary>
    public required Status Status { get; init; }

    /// <summary>
    /// The sender's message priority.
    /// </summary>
    public required sbyte? Priority { get; init; }

    /// <summary>
    /// The sender's capabilities.
    /// </summary>
    public required Remote<Capabilities> Capabilities { get; init; }

    public PresenceData Deduplicate()
    {
        return this == Empty ? Empty : this;
    }
}

public record struct CapabilitiesIdentifier(string Application, string Version, string VersionType);

public abstract record Capabilities(CapabilitiesIdentifier Identifier);
