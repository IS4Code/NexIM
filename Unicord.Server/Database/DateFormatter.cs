using System;
using System.Diagnostics.CodeAnalysis;
using MessagePack;
using MessagePack.Formatters;
using Unicord.Primitives;

namespace Unicord.Server.Database;

[SuppressMessage("Usage", "MsgPack013:Inaccessible formatter", Justification = "Explicit resolver")]
internal sealed class DateFormatter(IFormatterResolver standardResolver) : IMessagePackFormatter<DateComponents>
{
    readonly IMessagePackFormatter<DateTimeOffset> dateTimeOffset = standardResolver.GetFormatterWithVerify<DateTimeOffset>();

    public void Serialize(ref MessagePackWriter writer, DateComponents value, MessagePackSerializerOptions options)
    {
        // Components followed by time
        writer.WriteArrayHeader(2);
        writer.WriteUInt8((byte)value.Components);
        dateTimeOffset.Serialize(ref writer, value.Value, options);
    }

    public DateComponents Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.ReadArrayHeader() != 2)
        {
            throw new ArgumentException("Not a valid Date value.", nameof(reader));
        }
        var components = reader.ReadByte();
        return new(dateTimeOffset.Deserialize(ref reader, options), (DateComponentsCombination)components);
    }
}
