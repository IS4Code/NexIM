using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NexIM.Primitives;

[StructLayout(LayoutKind.Auto)]
public readonly record struct Number(Number.Unit Kind, float Value)
{
    static readonly NumberFormatInfo percentFormatInfo = CreatePercentFormatInfo();

    public Number(string text) : this(Parse(text, out var value), value)
    {
    }

    private static Unit Parse(string text, out float value)
    {
        if(Single.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return Unit.Integer;
        }
        else if(Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return Unit.Float;
        }
        else
        {
            value = Single.Parse(text, NumberStyles.Currency, percentFormatInfo);
            return Unit.Percent;
        }
    }

    public override string ToString() => Value.ToString(null, CultureInfo.InvariantCulture) + (Kind == Unit.Percent ? "%" : "");

    static NumberFormatInfo CreatePercentFormatInfo()
    {
        var info = CultureInfo.InvariantCulture.NumberFormat;
        return new() {
            CurrencyDecimalDigits = info.PercentDecimalDigits,
            CurrencyDecimalSeparator = info.PercentDecimalSeparator,
            CurrencyGroupSeparator = info.PercentGroupSeparator,
            CurrencyGroupSizes = info.PercentGroupSizes,
            CurrencyNegativePattern = info.PercentNegativePattern,
            CurrencyPositivePattern = info.PercentPositivePattern,
            CurrencySymbol = info.PercentSymbol
        };
    }

    public enum Unit
    {
        Zero,
        Float,
        Integer,
        Percent
    }
}
