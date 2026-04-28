using System;
using System.Globalization;
using System.Runtime.InteropServices;
using NexIM.Tools;

namespace NexIM.Primitives;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LanguageTaggedString : IComparable<LanguageTaggedString>
{
    readonly ValueString value;

    public string Value {
        get => value.Value;
        init => this.value = new(value);
    }

    public LanguageCode Language { get; init; }
    public bool Explicit { get; init; }

    public LanguageTaggedString(string value, LanguageCode language)
    {
        Value = value;
        Language = language;
    }

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

    public LanguageTaggedString(string value) : this(value, new(DefaultLanguage))
    {

    }

    public override string ToString()
    {
        return Value;
    }

    public int CompareTo(LanguageTaggedString other)
    {
        int cmp = Language.CompareTo(other.Language);
        if(cmp != 0)
        {
            return cmp;
        }
        return value.CompareTo(other.value);
    }
}
