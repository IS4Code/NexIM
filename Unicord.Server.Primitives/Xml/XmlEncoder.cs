using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Provides support for encoding to XML.
/// </summary>
public abstract class XmlEncoder : IValueXmlEncoder<TemporaryString>, IValueXmlEncoder<ArraySegment<byte>>, IValueXmlEncoder<TemporaryArray<byte>>
{
    delegate Task WriteStartAttributeAsyncDelegate(XmlWriter writer, string? prefix, string localName, string? ns);
    delegate Task WriteEndAttributeAsyncDelegate(XmlWriter writer);

    static readonly WriteStartAttributeAsyncDelegate writeStartAttributeAsync;
    static readonly WriteEndAttributeAsyncDelegate writeEndAttributeAsync;

    protected abstract XmlWriter Writer { get; }

    protected ValueTask Encode<T, TEncoder>(XmlWriter writer, T value, TEncoder encoder) where TEncoder : IValueXmlEncoder<T>
    {
        return encoder.Encode(writer, value);
    }

    protected Task WriteStartAttributeAsync(XmlWriter writer, string? prefix, string localName, string? ns)
    {
        return writeStartAttributeAsync(writer, prefix, localName, ns);
    }

    protected Task WriteEndAttributeAsync(XmlWriter writer)
    {
        return writeEndAttributeAsync(writer);
    }

    static XmlEncoder()
    {
        var writerType = typeof(XmlWriter);

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        var writeStart = writerType.GetMethod("WriteStartAttributeAsync", flags, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
        writeStartAttributeAsync = (WriteStartAttributeAsyncDelegate)Delegate.CreateDelegate(typeof(WriteStartAttributeAsyncDelegate), writeStart);

        var writeEnd = writerType.GetMethod("WriteEndAttributeAsync", flags, null, Type.EmptyTypes, null);
        writeEndAttributeAsync = (WriteEndAttributeAsyncDelegate)Delegate.CreateDelegate(typeof(WriteEndAttributeAsyncDelegate), writeEnd);
    }

    ValueTask IValueXmlEncoder<ArraySegment<byte>>.Encode(XmlWriter writer, ArraySegment<byte> value)
    {
        return new(writer.WriteBase64Async(value.Array!, value.Offset, value.Count));
    }

    async ValueTask IValueXmlEncoder<TemporaryString>.Encode(XmlWriter writer, TemporaryString value)
    {
        var segment = value.Value;
        await writer.WriteCharsAsync(segment.Array!, segment.Offset, segment.Count);
    }

    async ValueTask IValueXmlEncoder<TemporaryArray<byte>>.Encode(XmlWriter writer, TemporaryArray<byte> value)
    {
        var segment = value.Value;
        await writer.WriteBase64Async(segment.Array!, segment.Offset, segment.Count);
    }
}

public interface IValueXmlEncoder<in T>
{
    ValueTask Encode(XmlWriter writer, T value);
}
