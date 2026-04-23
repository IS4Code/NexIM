using System.Globalization;
using System.Runtime.InteropServices;

namespace NexIM.Primitives;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LanguageTaggedString(string Value, LanguageCode Language)
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

    public LanguageTaggedString(string value) : this(value, new(DefaultLanguage))
    {

    }

    public override string ToString()
    {
        return Value;
    }
}
