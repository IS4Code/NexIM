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

public record Message(string? Subject, string? Body);
