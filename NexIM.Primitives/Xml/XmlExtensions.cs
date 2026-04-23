using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace NexIM.Primitives.Xml;

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

    public static XmlReader WithAsyncSupport(this XmlReader reader)
    {
        if(reader is XmlAsyncOverSyncReader or { Settings.Async: true })
        {
            return reader;
        }
        return new XmlAsyncOverSyncReader(reader);
    }

    public static XmlWriter WithAsyncSupport(this XmlWriter writer)
    {
        if(writer is XmlAsyncOverSyncWriter or { Settings.Async: true })
        {
            return writer;
        }
        return new XmlAsyncOverSyncWriter(writer);
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

    public static async ValueTask<XElement> CaptureContent(this XmlReader reader)
    {
        var container = new XElement(
            "_",
            // Preserve implicit xml:lang environment
            new XAttribute(XNamespace.Xml + "lang", reader.XmlLang)
        );
        using(var writer = container.CreateWriter().WithAsyncSupport())
        {
            await writer.WriteNodeAsync(reader, false);
        }
        return container;
    }

    public static async ValueTask RestoreContent(this XElement container, Func<XmlReader, ValueTask> receiver)
    {
        using var reader = container.CreateReader().WithAsyncSupport();
        while(reader.Read())
        {
            if(reader.Depth == 1 && reader.NodeType != XmlNodeType.EndElement)
            {
                // Positioned on a node below the root <_>
                using var subtreeReader = reader.ReadSubtree();
                await receiver(subtreeReader);
                while(subtreeReader.Read())
                {
                    // Skip all unread nodes
                    subtreeReader.Skip();
                }
            }
        }
    }

    public static Task WriteNodeWithLanguageAsync(this XmlWriter writer, XmlReader reader, bool defattr)
    {
        if(String.Equals(writer.XmlLang ?? "", reader.XmlLang ?? "", StringComparison.OrdinalIgnoreCase))
        {
            // Compatible language
            return writer.WriteNodeAsync(reader, defattr);
        }

        return Inner();
        async Task Inner()
        {
            int depth = reader.NodeType == XmlNodeType.None ? -1 : reader.Depth;
            while(await reader.ReadAsync() && depth < reader.Depth)
            {
                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        await writer.WriteStartElementAsync(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                        await writer.WriteAttributesAsync(reader, defattr);
                        if(reader.Depth == depth + 1 && reader.GetAttribute("lang", XNamespace.Xml.NamespaceName) == null)
                        {
                            // Top-level element without xml:lang
                            await writer.WriteAttributeStringAsync("xml", "lang", XNamespace.Xml.NamespaceName, reader.XmlLang);
                        }
                        if(reader.IsEmptyElement)
                        {
                            await writer.WriteEndElementAsync();
                        }
                        break;
                    case XmlNodeType.Text:
                        if(!reader.CanReadValueChunk)
                        {
                            await writer.WriteStringAsync(await reader.GetValueAsync());
                            break;
                        }
                        var pool = ArrayPool<char>.Shared;
                        var array = pool.Rent(256);
                        try
                        {
                            int read;
                            while((read = await reader.ReadValueChunkAsync(array, 0, array.Length)) != 0)
                            {
                                await writer.WriteCharsAsync(array, 0, read);
                            }
                        }
                        finally
                        {
                            pool.Return(array);
                        }
                        break;
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        await writer.WriteWhitespaceAsync(await reader.GetValueAsync());
                        break;
                    case XmlNodeType.CDATA:
                        await writer.WriteCDataAsync(await reader.GetValueAsync());
                        break;
                    case XmlNodeType.EntityReference:
                        await writer.WriteEntityRefAsync(reader.Name);
                        break;
                    case XmlNodeType.XmlDeclaration:
                    case XmlNodeType.ProcessingInstruction:
                        await writer.WriteProcessingInstructionAsync(reader.Name, await reader.GetValueAsync());
                        break;
                    case XmlNodeType.DocumentType:
                        await writer.WriteDocTypeAsync(reader.Name, reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), await reader.GetValueAsync());
                        break;
                    case XmlNodeType.Comment:
                        await writer.WriteCommentAsync(await reader.GetValueAsync());
                        break;
                    case XmlNodeType.EndElement:
                        await writer.WriteFullEndElementAsync();
                        break;
                }
            }
            if(depth == reader.Depth && reader.NodeType == XmlNodeType.EndElement)
            {
                await reader.ReadAsync();
            }
        }
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
