using System.Diagnostics.CodeAnalysis;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Primitives;

namespace NexIM.Server.Database;

[SuppressMessage("Usage", "MsgPack013:Inaccessible formatter", Justification = "Explicit resolver")]
internal sealed class ValueUriFormatter(IFormatterResolver standardResolver) : IMessagePackFormatter<ValueUri>
{
    readonly IMessagePackFormatter<string> stringFormatter = standardResolver.GetFormatterWithVerify<string>();

    public void Serialize(ref MessagePackWriter writer, ValueUri value, MessagePackSerializerOptions options)
    {
        stringFormatter.Serialize(ref writer, value.ToString(), options);
    }

    public ValueUri Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return ValueUri.Parse(stringFormatter.Deserialize(ref reader, options));
    }
}
