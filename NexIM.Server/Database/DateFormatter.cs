using System;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Primitives;

namespace NexIM.Server.Database;

[ExcludeFormatterFromSourceGeneratedResolver]
internal sealed class DateFormatter(IFormatterResolver standardResolver) : IMessagePackFormatter<DateComponents>
{
    readonly IMessagePackFormatter<DateTimeOffset> dateTimeOffsetFormatter = standardResolver.GetFormatterWithVerify<DateTimeOffset>();

    public void Serialize(ref MessagePackWriter writer, DateComponents value, MessagePackSerializerOptions options)
    {
        // Components followed by time
        writer.WriteArrayHeader(2);
        writer.WriteUInt8((byte)value.Components);
        dateTimeOffsetFormatter.Serialize(ref writer, value.Value, options);
    }

    public DateComponents Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.ReadArrayHeader() != 2)
        {
            throw new ArgumentException("Not a valid Date value.", nameof(reader));
        }
        var components = reader.ReadByte();
        return new(dateTimeOffsetFormatter.Deserialize(ref reader, options), (DateComponentsCombination)components);
    }
}
