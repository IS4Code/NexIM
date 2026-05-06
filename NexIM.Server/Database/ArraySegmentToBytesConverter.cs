using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NexIM.Server.Database;

internal sealed class ArraySegmentToBytesConverter : ValueConverter<ArraySegment<byte>, byte[]>
{
    public ArraySegmentToBytesConverter() : base(
        x => Save(x),
        x => Load(x)
    )
    {
    }

    private static byte[] Save(ArraySegment<byte> value)
    {
        if(value.Count == 0)
        {
            return Array.Empty<byte>();
        }
        if(value.Offset == 0 && value.Count == value.Array!.Length)
        {
            return value.Array;
        }
        return value.ToArray();
    }

    private static ArraySegment<byte> Load(byte[]? value)
    {
        if(value == null)
        {
            return default;
        }
        return new(value);
    }
}
