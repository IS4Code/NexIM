using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using NexIM.Primitives;
using NexIM.Server.Accounts;
using NexIM.Tools;

namespace NexIM.Server.Events;

using MessageBodyCollectionData = ImmutableDictionary<(MessageFormat format, LanguageCode language), object>;

/// <summary>
/// Stores data for a delivered event.
/// </summary>
public abstract record DeliveryData : EventData
{
    /// <summary>
    /// The identifier of the entity that delayed the event.
    /// </summary>
    public required Identifier? DelayedBy { get; init; }

    /// <summary>
    /// The reason for a delayed delivery of this event.
    /// </summary>
    public required LanguageTaggedString? DelayReason { get; init; }

    /// <summary>
    /// Stores information about the additional addresses of this event.
    /// </summary>
    public required NonEmptySet<DeliveryAddress>? Addresses { get; init; }

    /// <summary>
    /// Stores the transaction identifier for a previous message that requested the receipt.
    /// </summary>
    public required string? ReceiptIdentifier { get; init; }
}

/// <summary>
/// Stores data for a message.
/// </summary>
public sealed record MessageData : DeliveryData
{
    /// <summary>
    /// The sender's presentation.
    /// </summary>
    public required SenderPresentation Presentation { get; init; }

    /// <summary>
    /// The subject of the message.
    /// </summary>
    public required LocalizedString? Subject { get; init; }

    /// <summary>
    /// The collection of message bodies in differing formats and languages.
    /// </summary>
    public required MessageBodyCollection Body { get; init; }

    /// <summary>
    /// The present state of the conversation.
    /// </summary>
    public required ConversationState State { get; init; }

    /// <summary>
    /// The identifier of the conversation.
    /// </summary>
    public required string? ThreadIdentifier { get; init; }

    /// <summary>
    /// The identifier of the parent conversation.
    /// </summary>
    public required string? ParentThreadIdentifier { get; init; }
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

    public static bool operator ==(MessageBodyCollection a, MessageBodyCollection b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(MessageBodyCollection a, MessageBodyCollection b)
    {
        return !a.Equals(b);
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

[StructLayout(LayoutKind.Auto)]
public readonly record struct DeliveryAddress(DeliveryAddressType Type, Identifier? Recipient, LanguageTaggedString? Description) : IComparable<DeliveryAddress>
{
    static readonly Comparer<Identifier?> recipientComparer = Comparer<Identifier?>.Default;
    static readonly Comparer<LanguageTaggedString?> descComparer = Comparer<LanguageTaggedString?>.Default;

    public static readonly DeliveryAddress DispositionNotification = new(DeliveryAddressType.DispositionNotify, null, null);

    public int CompareTo(DeliveryAddress other)
    {
        int cmp = ((int)Type).CompareTo((int)other.Type);
        if(cmp != 0)
        {
            return cmp;
        }
        cmp = recipientComparer.Compare(Recipient, other.Recipient);
        if(cmp != 0)
        {
            return cmp;
        }
        // TODO Should be a LocalizedString
        return descComparer.Compare(Description, other.Description);
    }
}

public enum DeliveryAddressType
{
    Primary,
    Secondary,
    Hidden,
    Reply,
    ReplyRoom,
    NoReply,
    Origin,
    DispositionNotify
}
