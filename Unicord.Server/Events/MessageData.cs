using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Unicord.Primitives;
using Unicord.Server.Accounts;

namespace Unicord.Server.Events;

using MessageBodyCollectionData = ImmutableDictionary<(MessageFormat format, LanguageCode language), object>;

/// <summary>
/// Stores data for a message.
/// </summary>
public sealed record MessageData : EventData
{
    /// <summary>
    /// The sender's presentation.
    /// </summary>
    public required SenderPresentation Presentation { get; set; }

    /// <summary>
    /// The subject of the message.
    /// </summary>
    public required LocalizedString Subject { get; set; }

    /// <summary>
    /// The collection of message bodies in differing formats and languages.
    /// </summary>
    public required MessageBodyCollection Body { get; set; }

    /// <summary>
    /// The present state of the conversation.
    /// </summary>
    public required ConversationState State { get; set; }

    /// <summary>
    /// The identifier of the conversation.
    /// </summary>
    public required string? ThreadIdentifier { get; set; }
}

/// <summary>
/// The format of a message's content.
/// </summary>
public enum MessageFormat
{
    /// <summary>
    /// Plain text without markup.
    /// </summary>
    Plain,

    /// <summary>
    /// Text with added formatting characters.
    /// </summary>
    Formatted,

    /// <summary>
    /// Text with HTML markup.
    /// </summary>
    Html
}

/// <summary>
/// The state of the conversation after a message.
/// </summary>
public enum ConversationState
{
    /// <summary>
    /// The conversation is not in any concrete state.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The conversation is started.
    /// </summary>
    Started,

    /// <summary>
    /// The conversation is active.
    /// </summary>
    Active,

    /// <summary>
    /// The conversation is inactive.
    /// </summary>
    Inactive,

    /// <summary>
    /// The conversation party has left.
    /// </summary>
    Gone,

    /// <summary>
    /// The conversation party is composing a message.
    /// </summary>
    Composing,

    /// <summary>
    /// The conversation party has paused composing a message.
    /// </summary>
    Paused
}

[StructLayout(LayoutKind.Auto)]
public readonly struct MessageBodyCollection(MessageBodyCollectionData data) : IEquatable<MessageBodyCollection>
{
    public static readonly MessageBodyCollection Empty = default;

    public MessageBodyCollectionData Data => data ?? MessageBodyCollectionData.Empty;

    public bool Equals(MessageBodyCollection other)
    {
        return
            Data.Count == other.Data.Count &&
            Data.All(other.Data.Contains);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is MessageBodyCollection body && Equals(body);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach(var pair in Data)
        {
            hashCode.Add(pair.Key.format);
            hashCode.Add(pair.Key.language);
            hashCode.Add(pair.Value);
        }
        return hashCode.ToHashCode();
    }
}
