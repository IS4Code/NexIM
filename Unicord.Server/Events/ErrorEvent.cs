using System.Net;
using Unicord.Primitives;
using Unicord.Server.Accounts;

namespace Unicord.Server.Events;

public sealed record ErrorEvent : Event<ErrorData>;

public record ErrorData : EventData
{
    public required StatusCode ErrorCode { get; set; }
    public required Identifier? Reporter { get; set; }
    public required RecommendedErrorAction RecommendedAction { get; set; }
    public required HttpStatusCode? HttpStatusCode { get; set; }
    public required LocalizedString Description { get; set; }
    public required EventData OriginalData { get; set; }
}

/// <summary>
/// The recommended action to take after receiving the error.
/// </summary>
public enum RecommendedErrorAction
{
    /// <summary>
    /// Indicates no handling of the error is needed.
    /// </summary>
    Proceed,

    /// <summary>
    /// Indicates the request must be modified before reattempting.
    /// </summary>
    Modify,

    /// <summary>
    /// Indicates the user lacks the appropriate authorization for performing the action.
    /// </summary>
    Authenticate,

    /// <summary>
    /// Indicates this action cannot be performed.
    /// </summary>
    Abandon,

    /// <summary>
    /// Indicates the error is only temporary.
    /// </summary>
    TryAgain
}
