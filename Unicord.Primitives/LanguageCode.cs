using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Unicord.Primitives;

/// <summary>
/// A language code used as a language tag.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct LanguageCode : IEquatable<LanguageCode>, IComparable<LanguageCode>
{
    readonly string? value;

    /// <summary>
    /// The string value of the language code.
    /// </summary>
    public string Value => value ?? String.Empty;

    /// <summary>
    /// Whether the value corresponds to a non-empty language code.
    /// </summary>
    public bool IsEmpty => String.IsNullOrEmpty(Value);

    /// <summary>
    /// Creates a new instance of the language code.
    /// </summary>
    /// <param name="value">The value of <see cref="Value"/>.</param>
    public LanguageCode(string value)
    {
        this.value = value;
    }

    /// <summary>
    /// Creates a new instance of the language code from a culture.
    /// </summary>
    /// <param name="culture">
    /// The culture representing the language, as <see cref="CultureInfo.IetfLanguageTag"/>.
    /// </param>
    public LanguageCode(CultureInfo culture) : this(CultureInfo.InvariantCulture.Equals(culture) ? String.Empty : culture.IetfLanguageTag)
    {

    }

    /// <summary>
    /// Returns <see cref="Value"/>.
    /// </summary>
    /// <returns><see cref="Value"/></returns>
    public override string ToString()
    {
        return Value;
    }

    /// <inheritdoc/>
    public bool Equals(LanguageCode other)
    {
        return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        return obj is LanguageCode code && Equals(code);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }

    /// <inheritdoc/>
    public int CompareTo(LanguageCode other)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(Value, other.Value);
    }

    /// <summary>
    /// Compares two instances of <see cref="LanguageCode"/> for equality.
    /// </summary>
    /// <param name="a">The first instance to compare.</param>
    /// <param name="b">The second instance to compare.</param>
    /// <returns>The result of <see cref="Equals(LanguageCode)"/>.</returns>
    public static bool operator ==(LanguageCode a, LanguageCode b)
    {
        return a.Equals(b);
    }

    /// <summary>
    /// Compares two instances of <see cref="LanguageCode"/> for inequality.
    /// </summary>
    /// <param name="a">The first instance to compare.</param>
    /// <param name="b">The second instance to compare.</param>
    /// <returns>The negated result of <see cref="Equals(LanguageCode)"/>.</returns>
    public static bool operator !=(LanguageCode a, LanguageCode b)
    {
        return !a.Equals(b);
    }
}