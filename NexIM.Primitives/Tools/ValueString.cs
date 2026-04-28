using System;
using System.Runtime.InteropServices;

namespace NexIM.Tools;

/// <summary>
/// Helper type to provide a default <see cref="String.Empty"/> value.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct ValueString : IEquatable<ValueString>, IComparable<ValueString>
{
    static readonly StringComparer comparer = StringComparer.Ordinal;

    readonly string? _value;

    public string Value => _value ?? String.Empty;

    public ValueString(string value)
    {
        _value = value;
    }

    public int CompareTo(ValueString other)
    {
        return comparer.Compare(Value, other.Value);
    }

    public bool Equals(ValueString other)
    {
        return comparer.Equals(Value, other.Value);
    }

    public override bool Equals(object obj)
    {
        return obj is ValueString other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(ValueString a, ValueString b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(ValueString a, ValueString b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return Value;
    }
}
