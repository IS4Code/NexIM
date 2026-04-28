using System;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Primitives;

namespace NexIM.Server.Database;

[ExcludeFormatterFromSourceGeneratedResolver]
internal sealed class TimeZoneOffsetFormatter(IFormatterResolver standardResolver) : IMessagePackFormatter<TimeZoneOffset>
{
    readonly IMessagePackFormatter<TimeSpan> timeSpanFormatter = standardResolver.GetFormatterWithVerify<TimeSpan>();

    public void Serialize(ref MessagePackWriter writer, TimeZoneOffset value, MessagePackSerializerOptions options)
    {
        timeSpanFormatter.Serialize(ref writer, value.Value, options);
    }

    public TimeZoneOffset Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return new(timeSpanFormatter.Deserialize(ref reader, options));
    }
}
