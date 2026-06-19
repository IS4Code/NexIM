using System;
using MessagePack;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexIM.Server.Events;

namespace NexIM.Server.Database;

internal sealed class EventExtensionsConverter : ValueConverter<EventExtensions, byte[]?>
{
    public EventExtensionsConverter(MessagePackSerializerOptions options) : base(
        x => Save(x, options),
        x => Load(x, options)
    )
    {
    }

    private static byte[] Save(EventExtensions value, MessagePackSerializerOptions options)
    {
        if(value.IsEmpty)
        {
            return Array.Empty<byte>();
        }
        return MessagePackSerializer.Serialize(value, options);
    }

    private static EventExtensions Load(byte[]? value, MessagePackSerializerOptions options)
    {
        if(value is null or { Length: 0 })
        {
            return default;
        }
        return MessagePackSerializer.Deserialize<EventExtensions>(value, options);
    }
}

internal sealed class EventExtensionsComparer : ValueComparer<EventExtensions>
{
    public EventExtensionsComparer() : base(
        (a, b) => Equals(a, b),
        x => GetHashCode(x))
    {
    }

    private static new bool Equals(EventExtensions first, EventExtensions second)
    {
        return first.Equals(second);
    }

    private static new int GetHashCode(EventExtensions value)
    {
        return value.GetHashCode();
    }
}
