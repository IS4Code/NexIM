using System;
using System.Runtime.InteropServices;
using NexIM.Primitives;
using NexIM.Server.Accounts;
using NexIM.Tools;

namespace NexIM.Server.Events;

/// <summary>
/// Stores data for a delivered event.
/// </summary>
public abstract record DeliveryData : EventData
{
    /// <summary>
    /// Stores information about the timing of the event.
    /// </summary>
    public DeliveryTiming? Timing { get; init; }

    /// <summary>
    /// Stores information about additional addresses related to this event.
    /// </summary>
    public NonEmptyDictionary<AddressRelation, LocalizedString?>? AddressRelations { get; init; }

    /// <summary>
    /// Stores information about additional messages related to this event.
    /// </summary>
    public NonEmptyDictionary<MessageRelation, LocalizedString?>? MessageRelations { get; init; }
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct DeliveryTiming(Identifier? ObservedBy, LanguageTaggedString? Description);

[StructLayout(LayoutKind.Auto)]
public readonly record struct AddressRelation(DeliveryRelationType Type, Identifier? Recipient) : IComparable<AddressRelation>
{
    public static readonly AddressRelation NoReply = new(DeliveryRelationType.NoReply, null);
    public static readonly AddressRelation NoStore = new(DeliveryRelationType.NoStore, null);
    public static readonly AddressRelation NoCopy = new(DeliveryRelationType.NoCopy, null);
    public static readonly AddressRelation NoPermanentStore = new(DeliveryRelationType.NoPermanentStore, null);
    public static readonly AddressRelation Store = new(DeliveryRelationType.Store, null);
    public static readonly AddressRelation DispositionNotification = new(DeliveryRelationType.DispositionNotify, null);
    public static readonly AddressRelation DisplayNotification = new(DeliveryRelationType.DisplayNotify, null);

    public int CompareTo(AddressRelation other)
    {
        int cmp = ((int)Type).CompareTo((int)other.Type);
        if(cmp != 0)
        {
            return cmp;
        }
        return Identifier.NullableComparer.Compare(Recipient, other.Recipient);
    }
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct MessageRelation(DeliveryRelationType Type, Identifier? Originator, string MessageIdentifier) : IComparable<MessageRelation>
{
    public int CompareTo(MessageRelation other)
    {
        int cmp = ((int)Type).CompareTo((int)other.Type);
        if(cmp != 0)
        {
            return cmp;
        }
        cmp = Identifier.NullableComparer.Compare(Originator, other.Originator);
        if(cmp != 0)
        {
            return cmp;
        }
        return StringComparer.Ordinal.Compare(MessageIdentifier, other.MessageIdentifier);
    }
}

public enum DeliveryRelationType
{
    Primary,
    Secondary,
    Hidden,
    Reply,
    ReplyRoom,
    Origin,
    NoReply,
    NoStore,
    NoCopy,
    NoPermanentStore,
    Store,
    DispositionNotify,
    DisplayNotify,
    Refer
}
