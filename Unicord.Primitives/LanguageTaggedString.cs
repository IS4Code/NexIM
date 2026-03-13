using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Unicord.Primitives;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LanguageTaggedString(string Value, string LanguageTag)
{
    public bool Explicit { get; init; }

    public static string DefaultLanguage {
        get {
            var culture = CultureInfo.CurrentUICulture;
            if(culture == CultureInfo.InvariantCulture)
            {
                return "en";
            }
            return culture.TwoLetterISOLanguageName;
        }
    }

    public LanguageTaggedString(string value) : this(value, DefaultLanguage)
    {

    }

    public bool Equals(LanguageTaggedString other)
    {
        return Value == other.Value && LanguageTag.Equals(other.LanguageTag, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(LanguageTag ?? "", StringComparer.OrdinalIgnoreCase);
        hashCode.Add(Value ?? "");
        return hashCode.ToHashCode();
    }

    public override string ToString()
    {
        return Value;
    }
}
