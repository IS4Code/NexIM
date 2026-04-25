using System;
using System.Xml;

namespace NexIM.Primitives;

public readonly record struct TimeZoneOffset(TimeSpan Value)
{
    public static readonly TimeZoneOffset Zero = new(TimeSpan.Zero);

    public static TimeZoneOffset FromDateTime(DateTime dateTime)
    {
        return FromDateTimeOffset(new DateTimeOffset(dateTime));
    }

    public static TimeZoneOffset FromDateTimeOffset(DateTimeOffset dateTimeOffset)
    {
        return new(dateTimeOffset.Offset);
    }

    public static implicit operator TimeZoneOffset(TimeSpan value) => new(value);
    public static implicit operator TimeSpan(TimeZoneOffset offset) => offset.Value;

    public static TimeZoneOffset Parse(string text)
    {
        if(text == "Z")
        {
            return Zero;
        }

        return new(XmlConvert.ToDateTimeOffset(text, "zzzzzzz").Offset);
    }

    public override string ToString()
    {
        var dateTime = new DateTimeOffset(62135596800 * TimeSpan.TicksPerSecond, Value);
        return XmlConvert.ToString(dateTime, "zzzzzzz");
    }
}
