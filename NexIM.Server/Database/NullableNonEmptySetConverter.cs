using System;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexIM.Tools;

namespace NexIM.Server.Database;

internal sealed class NullableNonEmptySetConverter<T> : ValueConverter<NonEmptySet<T>?, byte[]?> where T : IComparable<T>
{
    private NullableNonEmptySetConverter(MessagePackSerializerOptions options, ValueTuple _) : base(
        x => Save(x, options),
        x => Load(x, options)
    )
    {
    }

    public NullableNonEmptySetConverter(MessagePackSerializerOptions options) : this(WithFormatter(options), default(ValueTuple))
    {

    }

    private static byte[]? Save(NonEmptySet<T>? value, MessagePackSerializerOptions options)
    {
        if(value == null)
        {
            return null;
        }
        return MessagePackSerializer.Serialize(value, options);
    }

    private static NonEmptySet<T>? Load(byte[]? value, MessagePackSerializerOptions options)
    {
        if(value == null)
        {
            return null;
        }
        return MessagePackSerializer.Deserialize<NonEmptySet<T>?>(value, options);
    }

    static MessagePackSerializerOptions WithFormatter(MessagePackSerializerOptions options)
    {
        return options.WithResolver(
            CompositeResolver.Create(
                // Must go first to avoid unwrapping the element type
                new Resolver(options.Resolver),
                options.Resolver
            )
        );
    }

    sealed class Resolver(IFormatterResolver standardResolver) : IFormatterResolver, IResolver<NonEmptySet<T>?>
    {
        public IMessagePackFormatter<TOther>? GetFormatter<TOther>()
        {
            return (this as IResolver<TOther>)?.GetFormatter();
        }

        IMessagePackFormatter<NonEmptySet<T>?>? IResolver<NonEmptySet<T>?>.GetFormatter()
        {
            return new NullableNonEmptySetFormatter<T>(standardResolver);
        }
    }
}

file interface IResolver<TOther>
{
    IMessagePackFormatter<TOther>? GetFormatter();
}
