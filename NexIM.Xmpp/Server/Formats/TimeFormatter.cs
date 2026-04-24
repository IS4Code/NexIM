using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Xmpp.Protocol;

namespace NexIM.Xmpp.Server.Formats;

internal static class TimeFormatter
{
    public static async ValueTask WriteTo(this DateTimeOffset dateTimeOffset, ITimeHandler handler)
    {
        await handler.TimeZoneOffset(TimeZoneOffset.FromDateTimeOffset(dateTimeOffset));
        await handler.UtcTime(dateTimeOffset.UtcDateTime);
    }
}
