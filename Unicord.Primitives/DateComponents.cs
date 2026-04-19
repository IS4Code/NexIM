using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Unicord.Primitives;

/// <summary>
/// Represents an ISO 8601 date or date/time, with optional offset.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DateComponents(DateTimeOffset Value, DateComponentsCombination Components)
{
    static readonly string[][] formats;

    static readonly char[] timeOffsetChars = { '+', '-', 'Z' };
    static readonly char[] offsetChars = { '+', 'Z' };
    static readonly char[] dateChars = { '-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

    public static DateComponents Parse(string text)
    {
        var info = DateTimeFormatInfo.InvariantInfo;
        const DateTimeStyles styles = DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite;

        const DateComponentsCombination dateComponents = DateComponentsCombination.Year | DateComponentsCombination.Month | DateComponentsCombination.Day;
        const DateComponentsCombination timeComponents = DateComponentsCombination.Hour | DateComponentsCombination.Minute | DateComponentsCombination.Second | DateComponentsCombination.Fraction;

        var requiredAny1 = (DateComponentsCombination)Byte.MaxValue;
        var requiredAny2 = requiredAny1;
        DateComponentsCombination requiredAll = 0;
        var excludedAny = requiredAll;

        int timeAt = text.IndexOf('T');
        if(timeAt != -1)
        {
            // Must have any time component
            requiredAny1 = timeComponents;
            if(text.LastIndexOfAny(timeOffsetChars, timeAt + 1) != -1)
            {
                // Must have offset
                requiredAll = DateComponentsCombination.Offset;
            }
            else
            {
                // Must not have offset
                excludedAny |= DateComponentsCombination.Offset;
            }
            if(text.IndexOfAny(dateChars, 0, timeAt) != -1)
            {
                // Must have any date component
                requiredAny2 = dateComponents;
            }
            else
            {
                // Must not have any date component
                excludedAny |= dateComponents;
            }
        }
        else
        {
            // Must have date or offset and not any time component
            requiredAny1 = dateComponents | DateComponentsCombination.Offset;
            excludedAny |= timeComponents;
            int offsetAt = text.LastIndexOfAny(offsetChars);
            if(offsetAt != -1)
            {
                // Must have offset
                requiredAll = DateComponentsCombination.Offset;
                if(text.IndexOf('-', 0, offsetAt) != -1)
                {
                    // Must have date
                    requiredAny2 = dateComponents;
                }
            }
        }

        for(int i = formats.Length - 1; i >= 1; i--)
        {
            var combination = (DateComponentsCombination)i;
            if((combination & requiredAny1) != 0 && (combination & requiredAny2) != 0 && (combination & requiredAll) == requiredAll && (combination & excludedAny) == 0)
            {
                var format = formats[i];
                if(format == null)
                {
                    continue;
                }
                if(DateTimeOffset.TryParseExact(text, format, info, styles, out var result))
                {
                    // Success
                    return new(result, combination);
                }
            }
        }

        throw new FormatException();
    }

    public override string ToString()
    {
        return XmlConvert.ToString(Value, formats[(int)Components][0]);
    }

    static DateComponents()
    {
        var options = new[] { true, false };
        var array = new string[Byte.MaxValue + 1][];
        DateComponentsCombination combination = 0;

        foreach(var hasYear in options)
        {
            SetBit(DateComponentsCombination.Year, hasYear);
            foreach(var hasMonth in options)
            {
                SetBit(DateComponentsCombination.Month, hasMonth);
                foreach(var hasDay in options)
                {
                    SetBit(DateComponentsCombination.Day, hasDay);
                    foreach(var hasHour in options)
                    {
                        SetBit(DateComponentsCombination.Hour, hasHour);
                        foreach(var hasMinute in options)
                        {
                            SetBit(DateComponentsCombination.Minute, hasMinute);
                            foreach(var hasSecond in options)
                            {
                                SetBit(DateComponentsCombination.Second, hasSecond);
                                foreach(var hasFraction in options)
                                {
                                    SetBit(DateComponentsCombination.Fraction, hasFraction);
                                    foreach(var hasOffset in options)
                                    {
                                        SetBit(DateComponentsCombination.Offset, hasOffset);

                                        var forms = new HashSet<string>();

                                        var sb = new StringBuilder();
                                        bool hasDate = hasYear || hasMonth || hasDay;
                                        if(hasDate)
                                        {
                                            if(hasYear)
                                            {
                                                sb.Append("yyyy");
                                                if(hasMonth)
                                                {
                                                    sb.Append("-MM");
                                                    if(hasDay)
                                                    {
                                                        sb.Append("-dd");
                                                    }
                                                }
                                                else if(hasDay)
                                                {
                                                    // Missing in middle not supported
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                sb.Append("--");
                                                if(hasMonth)
                                                {
                                                    sb.Append("MM");
                                                    if(hasDay)
                                                    {
                                                        sb.Append("-dd");
                                                    }
                                                }
                                                else
                                                {
                                                    sb.Append("-dd");
                                                }
                                            }
                                        }
                                        if(hasHour || hasMinute || hasSecond || hasFraction)
                                        {
                                            sb.Append('T');
                                            if(hasHour)
                                            {
                                                sb.Append("HH");
                                                if(hasMinute)
                                                {
                                                    sb.Append(":mm");
                                                    if(hasSecond)
                                                    {
                                                        sb.Append(":ss");
                                                        if(hasFraction)
                                                        {
                                                            sb.Append(".FFFFFFF");
                                                        }
                                                    }
                                                    else if(hasFraction)
                                                    {
                                                        // Missing in middle not supported
                                                        continue;
                                                    }
                                                }
                                                else if(hasSecond || hasFraction)
                                                {
                                                    // Missing in middle not supported
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                sb.Append('-');
                                                if(hasMinute)
                                                {
                                                    sb.Append("mm");
                                                    if(hasSecond)
                                                    {
                                                        sb.Append(":ss");
                                                        if(hasFraction)
                                                        {
                                                            sb.Append(".FFFFFFF");
                                                        }
                                                    }
                                                    else if(hasFraction)
                                                    {
                                                        // Missing in middle not supported
                                                        continue;
                                                    }
                                                }
                                                else
                                                {
                                                    sb.Append('-');
                                                    if(hasSecond)
                                                    {
                                                        sb.Append("ss");
                                                        if(hasFraction)
                                                        {
                                                            sb.Append(".FFFFFFF");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        sb.Append("-.FFFFFFF");
                                                    }
                                                }
                                            }
                                        }
                                        else if(!hasDate)
                                        {
                                            // No date nor time, skip
                                            continue;
                                        }

                                        if(hasOffset)
                                        {
                                            // Try both Z and zzzzzz
                                            sb.Append('Z');
                                            forms.Add(sb.ToString());
                                            sb.Remove(sb.Length - 1, 1);
                                            sb.Append("zzzzzz");
                                            forms.Add(sb.ToString());
                                        }
                                        else
                                        {
                                            forms.Add(sb.ToString());
                                        }

                                        array[(byte)combination] = forms.ToArray();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        formats = array;

        void SetBit(DateComponentsCombination bit, bool set)
        {
            if(set)
            {
                combination |= bit;
            }
            else
            {
                combination &= ~bit;
            }
        }
    }
}

[Flags]
public enum DateComponentsCombination : byte
{
    Offset = 1,
    Fraction = 1 << 1,
    Second = 1 << 2,
    Minute = 1 << 3,
    Hour = 1 << 4,
    Day = 1 << 5,
    Month = 1 << 6,
    Year = 1 << 7
}
