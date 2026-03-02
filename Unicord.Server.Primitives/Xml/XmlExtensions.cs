using System.Reflection;
using System;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.InteropServices;
using System.Buffers;

namespace Unicord.Server.Primitives.Xml;

public static class XmlExtensions
{
    static readonly ArrayPool<char> pool = ArrayPool<char>.Shared;

    public static string? Get(this XmlNameTable nameTable, ReadOnlyMemory<char> memory)
    {
        if(nameTable is XmlMemoryNameTable memoryNameTable)
        {
            return memoryNameTable.Get(memory);
        }
        if(MemoryMarshal.TryGetArray(memory, out var segment))
        {
            return nameTable.Get(segment.Array, segment.Offset, segment.Count);
        }
        if(MemoryMarshal.TryGetString(memory, out var str, out var start, out var length) && start == 0 && length == str.Length)
        {
            return nameTable.Get(str);
        }
        return Get(nameTable, memory.Span);
    }

    public static string? Get(this XmlNameTable nameTable, ReadOnlySpan<char> span)
    {
        var array = pool.Rent(span.Length);
        try
        {
            span.CopyTo(array.AsSpan());
            return nameTable.Get(array, 0, span.Length);
        }
        finally
        {
            pool.Return(array);
        }
    }

    public static string Add(this XmlNameTable nameTable, ReadOnlyMemory<char> memory)
    {
        if(nameTable is XmlMemoryNameTable memoryNameTable)
        {
            return memoryNameTable.Add(memory);
        }
        if(MemoryMarshal.TryGetArray(memory, out var segment))
        {
            return nameTable.Add(segment.Array, segment.Offset, segment.Count);
        }
        if(MemoryMarshal.TryGetString(memory, out var str, out var start, out var length) && start == 0 && length == str.Length)
        {
            return nameTable.Add(str);
        }
        return Add(nameTable, memory.Span);
    }

    public static string Add(this XmlNameTable nameTable, ReadOnlySpan<char> span)
    {
        var array = pool.Rent(span.Length);
        try
        {
            span.CopyTo(array.AsSpan());
            return nameTable.Add(array, 0, span.Length);
        }
        finally
        {
            pool.Return(array);
        }
    }

    public static int ReadContentAsChars(this XmlReader reader, char[] buffer, int index, int count)
    {
        int read = reader.ReadValueChunk(buffer, index, count);
        if(read == 0 && count != 0)
        {
            // End of text
            if(reader.NodeType == XmlNodeType.Attribute)
            {
                reader.MoveToElement();
            }
            else
            {
                reader.Read();
            }
        }
        return read;
    }

    public static async ValueTask<int> ReadContentAsCharsAsync(this XmlReader reader, char[] buffer, int index, int count)
    {
        int read = await reader.ReadValueChunkAsync(buffer, index, count);
        if(read == 0 && count != 0)
        {
            // End of text
            if(reader.NodeType == XmlNodeType.Attribute)
            {
                reader.MoveToElement();
            }
            else
            {
                await reader.ReadAsync();
            }
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
