using System;

namespace Unicord.Primitives;

public readonly record struct TimeZoneOffset(TimeSpan Value)
{
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
}
