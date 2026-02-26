using Unicord.Server.Primitives;

namespace Unicord.Server.Model;

public enum ConversationType
{
    Normal,
    Chat,
    GroupChat,
    Headline,
    Error
}

public enum ChatState
{
    Started,
    Active,
    Inactive,
    Gone,
    Composing,
    Paused
}

public record Message(LocalizedString Subject, LocalizedString Body);
