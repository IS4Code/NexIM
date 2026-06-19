using System;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NexIM.Server.Database;

internal sealed class ArraySegmentToArrayConverter<T> : ValueConverter<ArraySegment<T>, T[]>
{
    public ArraySegmentToArrayConverter() : base(
        x => Save(x),
        x => Load(x)
    )
    {
    }

    private static T[] Save(ArraySegment<T> value)
    {
        if(value.Count == 0)
        {
            return Array.Empty<T>();
        }
        if(value.Offset == 0 && value.Count == value.Array!.Length)
        {
            return value.Array;
        }
        return value.ToArray();
    }

    private static ArraySegment<T> Load(T[]? value)
    {
        if(value == null)
        {
            return default;
        }
        return new(value);
    }
}

internal sealed class ArraySegmentComparer<T> : ValueComparer<ArraySegment<T>>
{
    public ArraySegmentComparer() : base(
        (a, b) => Equals(a, b),
        x => GetHashCode(x))
    {
    }

    private static new bool Equals(ArraySegment<T> first, ArraySegment<T> second)
    {
        return first.AsSpan().SequenceEqual(second);
    }

    private static new int GetHashCode(ArraySegment<T> value)
    {
        var hash = new HashCode();
        if(typeof(T) == typeof(byte))
        {
            var span = Unsafe.As<ArraySegment<T>, ArraySegment<byte>>(ref value).AsSpan();
            hash.AddBytes(span);
        }
        else
        {
            foreach(var obj in value)
            {
                hash.Add(obj);
            }
        }
        return hash.ToHashCode();
    }
}
