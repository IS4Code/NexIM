using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace NexIM.Primitives.Xml;

/// <summary>
/// Provides support for decoding from XML.
/// </summary>
public abstract class XmlDecoder :
    IValueXmlDecoder<TemporaryString>,
    IValueXmlDecoder<TemporaryUtf8String>,
    IValueXmlDecoder<ArraySegment<byte>>,
    IValueXmlDecoder<TemporaryArray<byte>>,
    IValueXmlDecoder<TemporaryFile>,
    IValueXmlDecoder<Token<Enum>>,
    IValueXmlDecoder<LanguageTaggedString>,
    IValueXmlDecoder<DateTime>,
    IValueXmlDecoder<DateTimeOffset>,
    IValueXmlDecoder<DateComponents>,
    IValueXmlDecoder<TimeZoneOffset>,
    IValueXmlDecoder<ValueUri>
{
    protected abstract void ThrowElementNotEmpty();
    protected abstract void ThrowElementNotSimple();

    public abstract string GetDefaultNamespace(XmlNameTable nameTable);

    protected async ValueTask EmptyElement(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            return;
        }

        await reader.ReadAsync();
        if(reader.NodeType != XmlNodeType.EndElement)
        {
            ThrowElementNotEmpty();
        }
    }

    protected async ValueTask<bool> OpenElement(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            // Known to be empty
            return false;
        }

        await reader.ReadAsync();
        switch(reader.NodeType)
        {
            case XmlNodeType.EndElement:
                return false;
            case XmlNodeType.Element:
                ThrowElementNotSimple();
                return false;
        }

        return true;
    }

    protected T CloseElement<T>(XmlReader reader, T result)
    {
        try
        {
            if(reader.NodeType != XmlNodeType.EndElement)
            {
                ThrowElementNotSimple();
            }
            return result;
        }
        catch when(Dispose())
        {
            throw;
        }

        bool Dispose()
        {
            (result as IDisposable)?.Dispose();
            return false;
        }
    }

    protected ValueTask<T> Decode<T, TDecoder>(XmlReader reader, TDecoder decoder) where TDecoder : IValueXmlDecoder<T>
    {
        return decoder.Decode(reader);
    }

    async ValueTask<ArraySegment<byte>> IValueXmlDecoder<ArraySegment<byte>>.Decode(XmlReader reader)
    {
        var pool = ArrayPool<byte>.Instance;
        var buffer = pool.Rent(1024);
        try
        {
            var stream = new MemoryStream();
            while(await reader.ReadContentAsBase64Async(buffer, 0, buffer.Length) is > 0 and var read)
            {
                stream.Write(buffer, 0, read);
            }
            if(!stream.TryGetBuffer(out var result))
            {
                result = new(stream.ToArray());
            }
            return result;
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    static readonly TemporaryArray<char>.AsynchronousReader<XmlReader> xmlTemporaryCharReader = static (buffer, reader) => {
        return reader.ReadContentAsCharsAsync(buffer.Array!, buffer.Offset, buffer.Count);
    };

    static readonly TemporaryArray<byte>.AsynchronousReader<XmlReader> xmlTemporaryByteReader = static (buffer, reader) => {
        return new(reader.ReadContentAsBase64Async(buffer.Array!, buffer.Offset, buffer.Count));
    };

    async ValueTask<TemporaryString> IValueXmlDecoder<TemporaryString>.Decode(XmlReader reader)
    {
        var str = new TemporaryString(arraySource: ArraySource<char>.Instance);
        try
        {
            await str.ReadFromAsync(xmlTemporaryCharReader, reader);
            return str;
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            throw;
        }

        bool Dispose()
        {
            str.Dispose();
            return false;
        }
    }

    async ValueTask<TemporaryUtf8String> IValueXmlDecoder<TemporaryUtf8String>.Decode(XmlReader reader)
    {
        var str = new TemporaryUtf8String(arraySource: ArraySource<char>.Instance);
        try
        {
            await str.ReadFromAsync(xmlTemporaryByteReader, reader);
            return str;
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            throw;
        }

        bool Dispose()
        {
            str.Dispose();
            return false;
        }
    }

    async ValueTask<TemporaryArray<byte>> IValueXmlDecoder<TemporaryArray<byte>>.Decode(XmlReader reader)
    {
        var arr = new TemporaryArray<byte>(arraySource: ArraySource<byte>.Instance);
        try
        {
            await arr.ReadFromAsync(xmlTemporaryByteReader, reader);
            return arr;
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            throw;
        }

        bool Dispose()
        {
            arr.Dispose();
            return false;
        }
    }

    ValueTask<TemporaryFile> IValueXmlDecoder<TemporaryFile>.Decode(XmlReader reader)
    {
        return TemporaryFile.ReadFromAsync(StorageQuota.Local, xmlTemporaryByteReader, reader);
    }

    protected async ValueTask<string> DecodeTokenAsync(XmlReader reader)
    {
        int start, total;

        if(!reader.CanReadValueChunk)
        {
            // Obtain value directly
            var value = await reader.GetValueAsync();
            await reader.ReadAsync();
            total = value.Length;
            Trim(value.AsSpan());
            return reader.NameTable.Add(value.AsMemory(0, total));
        }

        var pool = ArrayPool<char>.Instance;
        var array = pool.Rent(16);
        try
        {
            total = 0;

            // Read input chunks into a contiguous array

            int read;
            while((read = await reader.ReadContentAsCharsAsync(array, total, array.Length - total)) != 0)
            {
                total += read;
                if(total == array.Length)
                {
                    // Rent a larger array (will pick an exponentially larger bucket)
                    var larger = pool.Rent(array.Length + 1);
                    array.CopyTo(larger, 0);
                    pool.Return(array);
                    array = larger;
                }
            }

            // Trim whitespace
            start = 0;
            Trim(array.AsSpan(0, total));

            return reader.NameTable.Add(array, start, total);
        }
        finally
        {
            pool.Return(array);
        }

        void Trim(ReadOnlySpan<char> span)
        {
            var trimmed = span.Trim(" \r\n\t".AsSpan());
            if(trimmed.Length == span.Length)
            {
                return;
            }
            if(span.Overlaps(trimmed, out start))
            {
                total = trimmed.Length;
            }
        }
    }

    async ValueTask<Token<Enum>> IValueXmlDecoder<Token<Enum>>.Decode(XmlReader reader)
    {
        return Token<Enum>.FromAtomized(await DecodeTokenAsync(reader));
    }

    async ValueTask<LanguageTaggedString> IValueXmlDecoder<LanguageTaggedString>.Decode(XmlReader reader)
    {
        var (language, isExplicit) =
            reader.GetAttribute("lang", "http://www.w3.org/XML/1998/namespace") is { } lang
            ? (lang, true)
            : (reader.XmlLang, false);
        return new(await reader.ReadContentAsStringAsync(), new(language)) {
            Explicit = isExplicit
        };
    }

    async ValueTask<DateTime> IValueXmlDecoder<DateTime>.Decode(XmlReader reader)
    {
        var dateTime = XmlConvert.ToDateTime(await reader.ReadContentAsStringAsync(), XmlDateTimeSerializationMode.RoundtripKind);
        if(dateTime.Kind != DateTimeKind.Utc)
        {
            throw new FormatException("Date must be in UTC.");
        }
        return dateTime;
    }

    async ValueTask<DateTimeOffset> IValueXmlDecoder<DateTimeOffset>.Decode(XmlReader reader)
    {
        return XmlConvert.ToDateTimeOffset(await reader.ReadContentAsStringAsync());
    }

    async ValueTask<DateComponents> IValueXmlDecoder<DateComponents>.Decode(XmlReader reader)
    {
        return DateComponents.Parse(await reader.ReadContentAsStringAsync());
    }

    async ValueTask<TimeZoneOffset> IValueXmlDecoder<TimeZoneOffset>.Decode(XmlReader reader)
    {
        return TimeZoneOffset.Parse(await reader.ReadContentAsStringAsync());
    }

    static readonly char[] whitespace = { ' ', '\t', '\n', '\r' };

    async ValueTask<ValueUri> IValueXmlDecoder<ValueUri>.Decode(XmlReader reader)
    {
        var uri = await reader.ReadContentAsStringAsync();

        if(!String.IsNullOrEmpty(uri))
        {
            uri = uri.Trim(whitespace);
        }

        return ValueUri.Parse(uri);
    }

    static class ArrayPool<T>
    {
        public static readonly System.Buffers.ArrayPool<T> Instance = System.Buffers.ArrayPool<T>.Create();
    }

    sealed class ArraySource<T> : TemporaryArraySource<T> where T : unmanaged
    {
        public static readonly ArraySource<T> Instance = new();

        private ArraySource() : base(ArrayPool<T>.Instance)
        {

        }

        public override void ZeroMemory(Span<T> span)
        {
#if NETSTANDARD2_1_OR_GREATER
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(MemoryMarshal.Cast<T, byte>(span));
#else
            base.ZeroMemory(span);
#endif
        }
    }
}

public interface IValueXmlDecoder<T>
{
    ValueTask<T> Decode(XmlReader reader);
}
