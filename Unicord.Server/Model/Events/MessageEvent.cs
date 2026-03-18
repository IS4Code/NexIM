namespace Unicord.Server.Model.Events;

/// <summary>
/// Represents a message event. Such an event is suitable for long-term storage,
/// delayed delivery or reception by multiple parties.
/// </summary>
public sealed record MessageEvent : Event<MessageData>
{
    /// <summary>
    /// The type of the message.
    /// </summary>
    public required MessageType Type { get; init; }
}

/// <summary>
/// Represents the type of a message.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// The type is the message is not defined.
    /// </summary>
    Unspecified,

    /// <summary>
    /// Standard isolated message.
    /// </summary>
    Normal,

    /// <summary>
    /// A message produced as a part of a pre-existing chat session between two parties.
    /// </summary>
    Chat,

    /// <summary>
    /// A message produced in a multi-user chat room.
    /// </summary>
    GroupChat,

    /// <summary>
    /// A message that is a notification or alert that is temporal in nature.
    /// </summary>
    Headline
}
