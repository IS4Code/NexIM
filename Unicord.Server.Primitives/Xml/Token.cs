using System;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Represents an XML vocabulary token that can be compared by-reference
/// to a string obtained from an <see cref="XmlNameTable"/> instance.
/// </summary>
/// <typeparam name="TEnum">The schema enum type of the string.</typeparam>
public readonly record struct Token<TEnum> where TEnum : Enum
{
    readonly string? value;

    public string Value => value ?? "";

    private Token(string value)
    {
        this.value = value;
    }

    public bool Equals(Token<TEnum> obj) => ReferenceEquals(Value, obj.Value);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Value);

    public override string ToString()
    {
        return Value;
    }

    public static Token<TEnum> FromAtomized(string atomizedValue)
    {
        return new Token<TEnum>(atomizedValue);
    }

    public static bool operator ==(string a, Token<TEnum> b)
    {
        return ReferenceEquals(a, b.Value);
    }

    public static bool operator !=(string a, Token<TEnum> b)
    {
        return !ReferenceEquals(a, b.Value);
    }

    public static bool operator ==(Token<TEnum> a, string b)
    {
        return ReferenceEquals(a.Value, b);
    }

    public static bool operator !=(Token<TEnum> a, string b)
    {
        return !ReferenceEquals(a.Value, b);
    }
}
