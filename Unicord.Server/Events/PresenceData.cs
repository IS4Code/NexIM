using Unicord.Primitives;
using Unicord.Server.Accounts;

namespace Unicord.Server.Events;

/// <summary>
/// Stores data for a presence.
/// </summary>
public sealed record PresenceData : EventData
{
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
    public required CapabilitiesHandle? Capabilities { get; init; }
}

public record struct CapabilitiesIdentifier(string Application, string Version, string VersionType);

public record struct CapabilitiesHandle(CapabilitiesIdentifier Identifier, Cached<ICapabilities> Value);

public interface ICapabilities
{

}
