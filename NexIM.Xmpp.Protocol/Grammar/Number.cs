using System;
using System.Globalization;

namespace NexIM.Xmpp.Protocol.Grammar;

public readonly record struct Number
{
    static readonly NumberFormatInfo percentFormatInfo = CreatePercentFormatInfo();

    readonly ExCSS.Number value;

    public Number(string text)
    {
        if(Single.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
        {
            value = new(num, ExCSS.Number.Unit.Integer);
        }
        else if(Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out num))
        {
            value = new(num, ExCSS.Number.Unit.Float);
        }
        else
        {
            num = Single.Parse(text, NumberStyles.Currency, percentFormatInfo);
            value = new(num, ExCSS.Number.Unit.Percent);
        }
    }

    public override string ToString() => value.ToString(null, CultureInfo.InvariantCulture);

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
}
