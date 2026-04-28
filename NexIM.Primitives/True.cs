using System;
using System.Runtime.InteropServices;

namespace NexIM.Primitives;

/// <summary>
/// Represents a fixed <c><see langword="true"/></c> value.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct True
{
    public static True Parse(string str)
    {
        if(!"true".Equals(str, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException();
        }
        return default;
    }

    public override int GetHashCode()
    {
        return true.GetHashCode();
    }

    public static implicit operator bool(True _) => true;
    public static bool operator true(True _) => true;
    public static bool operator false(True _) => false;

    public override string ToString()
    {
        return "true";
    }
}
