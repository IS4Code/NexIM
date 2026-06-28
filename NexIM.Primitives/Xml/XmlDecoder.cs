using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives.Tools;

namespace NexIM.Primitives.Xml;

/// <summary>
/// Provides support for decoding from XML.
/// </summary>
public abstract class XmlDecoder :
    IValueXmlDecoder<TemporaryString>,
    IValueXmlDecoder<TemporaryUtf8String>,
    IValueXmlDecoder<Base64<ArraySegment<byte>>>,
    IValueXmlDecoder<Hex<ArraySegment<byte>>>,
    IValueXmlDecoder<Base64<TemporaryArray<byte>>>,
    IValueXmlDecoder<Hex<TemporaryArray<byte>>>,
    IValueXmlDecoder<Base64<TemporaryFile>>,
    IValueXmlDecoder<Hex<TemporaryFile>>,
    IValueXmlDecoder<IReadOnlyList<string>>,
    IValueXmlDecoder<Token<Enum>>,
    IValueXmlDecoder<LanguageCode>,
    IValueXmlDecoder<LanguageTaggedString>,
    IValueXmlDecoder<DateTime>,
    IValueXmlDecoder<DateTimeOffset>,
    IValueXmlDecoder<TimeSpan>,
    IValueXmlDecoder<DateComponents>,
    IValueXmlDecoder<TimeZoneOffset>,
    IValueXmlDecoder<ValueUri>,
    IValueXmlDecoder<MailAddress>,
    IValueXmlDecoder<True>
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

    async ValueTask<Base64<ArraySegment<byte>>> IValueXmlDecoder<Base64<ArraySegment<byte>>>.Decode(XmlReader reader)
    {
        return await Read(xmlTemporaryBase64Reader, reader);
    }

    async ValueTask<Hex<ArraySegment<byte>>> IValueXmlDecoder<Hex<ArraySegment<byte>>>.Decode(XmlReader reader)
    {
        return await Read(xmlTemporaryHexReader, reader);
    }

    async ValueTask<ArraySegment<byte>> Read<TArgs>(TemporaryArray<byte>.AsynchronousReader<TArgs> reader, TArgs args)
    {
        var pool = ArrayPool<byte>.Instance;
        var buffer = pool.Rent(1024);
        try
        {
            var stream = new MemoryStream();
            while(await reader(new(buffer, 0, buffer.Length), args) is > 0 and var read)
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

    static readonly TemporaryArray<byte>.AsynchronousReader<XmlReader> xmlTemporaryBase64Reader = static (buffer, reader) => {
        return new(reader.ReadContentAsBase64Async(buffer.Array!, buffer.Offset, buffer.Count));
    };

    static readonly TemporaryArray<byte>.AsynchronousReader<XmlReader> xmlTemporaryHexReader = static (buffer, reader) => {
        return new(reader.ReadContentAsBinHexAsync(buffer.Array!, buffer.Offset, buffer.Count));
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
            await str.ReadFromAsync(xmlTemporaryBase64Reader, reader);
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

    async ValueTask<Base64<TemporaryArray<byte>>> IValueXmlDecoder<Base64<TemporaryArray<byte>>>.Decode(XmlReader reader)
    {
        return await TemporaryArray<byte>.CreateFromAsync(xmlTemporaryBase64Reader, reader, arraySource: ArraySource<byte>.Instance);
    }

    async ValueTask<Hex<TemporaryArray<byte>>> IValueXmlDecoder<Hex<TemporaryArray<byte>>>.Decode(XmlReader reader)
    {
        return await TemporaryArray<byte>.CreateFromAsync(xmlTemporaryHexReader, reader, arraySource: ArraySource<byte>.Instance);
    }

    async ValueTask<Base64<TemporaryFile>> IValueXmlDecoder<Base64<TemporaryFile>>.Decode(XmlReader reader)
    {
        return await TemporaryFile.ReadFromAsync(StorageQuota.Local, xmlTemporaryBase64Reader, reader);
    }

    async ValueTask<Hex<TemporaryFile>> IValueXmlDecoder<Hex<TemporaryFile>>.Decode(XmlReader reader)
    {
        return await TemporaryFile.ReadFromAsync(StorageQuota.Local, xmlTemporaryHexReader, reader);
    }

    async ValueTask<IReadOnlyList<string>> IValueXmlDecoder<IReadOnlyList<string>>.Decode(XmlReader reader)
    {
        int start, total;

        if(!reader.CanReadValueChunk)
        {
            // Obtain value directly
            var value = await reader.GetValueAsync();
            await reader.ReadAsync();
            return value.Split(whitespace, StringSplitOptions.RemoveEmptyEntries);
        }

        var result = new List<string>();

        var pool = ArrayPool<char>.Instance;
        var array = pool.Rent(16);
        try
        {
            start = 0;
            total = 0;

            // Read input chunks into a contiguous array

            int read;
            while((read = await reader.ReadContentAsCharsAsync(array, total, array.Length - total)) != 0)
            {
                total += read;

                // Skip initial whitespace
                TrimStart(array.AsSpan(start, total));

                if(total == 0)
                {
                    // Only whitespace
                    start = 0;
                    total = 0;
                    continue;
                }

                int tokenEnd = array.AsSpan(start, total).IndexOfAny(whitespace);
                if(tokenEnd != -1)
                {
                    // Found individual token
                    result.Add(array.AsSpan(start, tokenEnd).ToString());

                    // Move the remainder to the beginning
                    int next = start + tokenEnd;
                    total -= next;
                    array.AsSpan(next, total).CopyTo(array.AsSpan(0, total));
                    start = 0;
                }

                if(total == array.Length)
                {
                    // Rent a larger array (will pick an exponentially larger bucket)
                    var larger = pool.Rent(array.Length + 1);
                    array.CopyTo(larger, 0);
                    pool.Return(array);
                    array = larger;
                }
            }

            if(total > 0)
            {
                // Unterminated token remaining
                result.Add(array.AsSpan(start, total).ToString());
            }

            return result;
        }
        finally
        {
            pool.Return(array);
        }

        void TrimStart(ReadOnlySpan<char> span)
        {
            var trimmed = span.TrimStart(whitespace.AsSpan());
            int difference = span.Length - trimmed.Length;
            start += difference;
            total -= difference;
        }
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
            var trimmed = span.Trim(whitespace.AsSpan());
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

    async ValueTask<LanguageCode> IValueXmlDecoder<LanguageCode>.Decode(XmlReader reader)
    {
        return new(await DecodeTokenAsync(reader));
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

    async ValueTask<TimeSpan> IValueXmlDecoder<TimeSpan>.Decode(XmlReader reader)
    {
        var content = await reader.ReadContentAsStringAsync();
        if(content.IndexOf('P') == -1 && TimeSpan.TryParseExact(content, "c", CultureInfo.InvariantCulture, out var timeSpan))
        {
            return timeSpan;
        }
        return XmlConvert.ToTimeSpan(content);
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
        var str = await reader.ReadContentAsStringAsync();

        if(!String.IsNullOrEmpty(str))
        {
            str = str.Trim(whitespace);
        }

        return ValueUri.Parse(str);
    }

    async ValueTask<MailAddress> IValueXmlDecoder<MailAddress>.Decode(XmlReader reader)
    {
        var str = await reader.ReadContentAsStringAsync();

        if(!String.IsNullOrEmpty(str))
        {
            str = str.Trim(whitespace);
        }

        FormatHelper.ValidateEmailAddress(str);
        return new MailAddress(str);
    }

    async ValueTask<True> IValueXmlDecoder<True>.Decode(XmlReader reader)
    {
        return True.Parse((await reader.ReadContentAsStringAsync()).Trim(whitespace));
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
