using System.Runtime.CompilerServices;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Represents an XML vocabulary token that can be compared by-reference
/// to a string obtained from an <see cref="XmlNameTable"/> instance.
/// </summary>
/// <param name="Value">The string value of the token.</param>
public readonly record struct Token(string Value)
{
    public bool Equals(Token obj) => ReferenceEquals(Value, obj.Value);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Value);

    public static bool operator ==(string a, Token b)
    {
        return ReferenceEquals(a, b.Value);
    }

    public static bool operator !=(string a, Token b)
    {
        return !ReferenceEquals(a, b.Value);
    }

    public static bool operator ==(Token a, string b)
    {
        return ReferenceEquals(a.Value, b);
    }

    public static bool operator !=(Token a, string b)
    {
        return !ReferenceEquals(a.Value, b);
    }

    public static implicit operator string(Token obj)
    {
        return obj.Value;
    }
}
