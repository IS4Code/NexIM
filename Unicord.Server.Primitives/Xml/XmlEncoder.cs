using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Provides support for encoding to XML.
/// </summary>
public abstract class XmlEncoder
{
    delegate Task WriteStartAttributeAsyncDelegate(XmlWriter writer, string? prefix, string localName, string? ns);
    delegate Task WriteEndAttributeAsyncDelegate(XmlWriter writer);

    static readonly WriteStartAttributeAsyncDelegate writeStartAttributeAsync;
    static readonly WriteEndAttributeAsyncDelegate writeEndAttributeAsync;

    protected readonly TypedEncoder TypedEncoder = TypedEncoder.Default;

    protected abstract XmlWriter Writer { get; }

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
}
