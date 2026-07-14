using System;
using System.Runtime.InteropServices;

namespace NexIM.Primitives.Tools;

internal static class HashCodeExtensions
{
    public static void AddBytes(this HashCode hashCode, ReadOnlySpan<byte> value)
    {
        while(value.Length >= sizeof(int))
        {
            hashCode.Add(MemoryMarshal.Cast<byte, int>(value)[0]);
            value = value.Slice(sizeof(int));
        }

        foreach(byte b in value)
        {
            hashCode.Add((int)b);
        }
    }
}
