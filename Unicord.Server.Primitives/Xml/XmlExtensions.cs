using System.Reflection;
using System;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

public static class XmlExtensions
{
    public static int ReadContentAsChars(this XmlReader reader, char[] buffer, int index, int count)
    {
        int read = reader.ReadValueChunk(buffer, index, count);
        if(read == 0 && count == 0)
        {
            // End of text
            reader.Read();
        }
        return read;
    }

    public static async ValueTask<int> ReadContentAsCharsAsync(this XmlReader reader, char[] buffer, int index, int count)
    {
        int read = await reader.ReadValueChunkAsync(buffer, index, count);
        if(read == 0 && count == 0)
        {
            // End of text
            await reader.ReadAsync();
        }
        return read;
    }

    delegate Task WriteStartAttributeAsyncDelegate(XmlWriter writer, string? prefix, string localName, string? ns);
    delegate Task WriteEndAttributeAsyncDelegate(XmlWriter writer);

    static readonly WriteStartAttributeAsyncDelegate writeStartAttributeAsync;
    static readonly WriteEndAttributeAsyncDelegate writeEndAttributeAsync;

    public static Task WriteStartAttributeAsync(this XmlWriter writer, string? prefix, string localName, string? ns)
    {
        return writeStartAttributeAsync(writer, prefix, localName, ns);
    }

    public static Task WriteEndAttributeAsync(this XmlWriter writer)
    {
        return writeEndAttributeAsync(writer);
    }

    static XmlExtensions()
    {
        var writerType = typeof(XmlWriter);

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        var writeStart = writerType.GetMethod("WriteStartAttributeAsync", flags, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
        writeStartAttributeAsync = (WriteStartAttributeAsyncDelegate)Delegate.CreateDelegate(typeof(WriteStartAttributeAsyncDelegate), writeStart);

        var writeEnd = writerType.GetMethod("WriteEndAttributeAsync", flags, null, Type.EmptyTypes, null);
        writeEndAttributeAsync = (WriteEndAttributeAsyncDelegate)Delegate.CreateDelegate(typeof(WriteEndAttributeAsyncDelegate), writeEnd);
    }
}
