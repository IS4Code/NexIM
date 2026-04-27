using System;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Tools;

namespace NexIM.Server.Database;

internal abstract class NullableNonEmptySetFormatterBase<T>(IFormatterResolver standardResolver) where T : IComparable<T>
{
    readonly IMessagePackFormatter<T> valueFormatter = standardResolver.GetFormatterWithVerify<T>();

    public static IMessagePackFormatter<NonEmptySet<T>?> Create(IFormatterResolver standardResolver)
    {
        if(typeof(string).Equals(typeof(T)))
        {
            // Must have an explicit instantiation because of #2242 in MessagePack-CSharp
            return (IMessagePackFormatter<NonEmptySet<T>?>)(object)(new NullableNonEmptyStringSetFormatter(standardResolver));
        }
        throw new NotSupportedException();
    }

    public void Serialize(ref MessagePackWriter writer, NonEmptySet<T>? value, MessagePackSerializerOptions options)
    {
        if(value is not { } set)
        {
            writer.WriteArrayHeader(0);
            return;
        }
        writer.WriteArrayHeader(set.Count);
        foreach(var item in set)
        {
            valueFormatter.Serialize(ref writer, item, options);
        }
    }

    public NonEmptySet<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.TryReadNil())
        {
            return null;
        }
        var count = reader.ReadArrayHeader();
        var builder = NonEmptySet<T>.Builder.Empty;
        for(int i = 0; i < count;  i++)
        {
            builder.Add(valueFormatter.Deserialize(ref reader, options));
        }
        return builder.TryToSet();
    }
}

[ExcludeFormatterFromSourceGeneratedResolver]
sealed file class NullableNonEmptyStringSetFormatter(IFormatterResolver standardResolver) : NullableNonEmptySetFormatterBase<string>(standardResolver), IMessagePackFormatter<NonEmptySet<string>?>
{

}
