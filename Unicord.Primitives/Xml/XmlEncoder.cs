using System;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Unicord.Primitives.Xml;

/// <summary>
/// Provides support for encoding to XML.
/// </summary>
public abstract class XmlEncoder :
    IValueXmlEncoder<TemporaryString>,
    IValueXmlEncoder<TemporaryUtf8String>,
    IValueXmlEncoder<ArraySegment<byte>>,
    IValueXmlEncoder<TemporaryArray<byte>>,
    IValueXmlEncoder<TemporaryFile>,
    IValueXmlEncoder<Token<Enum>>,
    IValueXmlEncoder<LanguageTaggedString>,
    IValueXmlEncoder<DateTime>,
    IValueXmlEncoder<DateTimeOffset>,
    IValueXmlEncoder<DateComponents>,
    IValueXmlEncoder<TimeZoneOffset>,
    IValueXmlEncoder<Uri>
{
    protected abstract XmlWriter Writer { get; }

    public abstract string? DefaultNamespace { get; }

    protected ValueTask Encode<T, TEncoder>(XmlWriter writer, T value, TEncoder encoder) where TEncoder : IValueXmlEncoder<T>
    {
        return encoder.Encode(writer, value);
    }

    protected Task WriteStartAttributeAsync(XmlWriter writer, string? prefix, string localName, string? ns)
    {
        return writer.WriteStartAttributeAsync(prefix, localName, ns);
    }

    protected Task WriteEndAttributeAsync(XmlWriter writer)
    {
        return writer.WriteEndAttributeAsync();
    }

    static readonly TemporaryArray<char>.AsynchronousWriter<XmlWriter> xmlTemporaryCharWriter = static (buffer, writer) => {
        return new(writer.WriteCharsAsync(buffer.Array!, buffer.Offset, buffer.Count));
    };

    static readonly TemporaryArray<byte>.AsynchronousWriter<XmlWriter> xmlTemporaryByteWriter = static (buffer, writer) => {
        return new(writer.WriteBase64Async(buffer.Array!, buffer.Offset, buffer.Count));
    };

    ValueTask IValueXmlEncoder<ArraySegment<byte>>.Encode(XmlWriter writer, ArraySegment<byte> value)
    {
        return xmlTemporaryByteWriter(value, writer);
    }

    ValueTask IValueXmlEncoder<TemporaryString>.Encode(XmlWriter writer, TemporaryString value)
    {
        return value.WriteToAsync(xmlTemporaryCharWriter, writer);
    }

    ValueTask IValueXmlEncoder<TemporaryUtf8String>.Encode(XmlWriter writer, TemporaryUtf8String value)
    {
        return value.WriteToAsync(xmlTemporaryByteWriter, writer);
    }

    ValueTask IValueXmlEncoder<TemporaryArray<byte>>.Encode(XmlWriter writer, TemporaryArray<byte> value)
    {
        return value.WriteToAsync(xmlTemporaryByteWriter, writer);
    }

    ValueTask IValueXmlEncoder<TemporaryFile>.Encode(XmlWriter writer, TemporaryFile value)
    {
        return value.WriteToAsync(xmlTemporaryByteWriter, writer);
    }

    protected async ValueTask EncodeTokenAsync(XmlWriter writer, string tokenValue)
    {
        await writer.WriteStringAsync(tokenValue);
    }

    ValueTask IValueXmlEncoder<Token<Enum>>.Encode(XmlWriter writer, Token<Enum> token)
    {
        return EncodeTokenAsync(writer, token.Value);
    }

    async ValueTask IValueXmlEncoder<LanguageTaggedString>.Encode(XmlWriter writer, LanguageTaggedString value)
    {
        if(!value.Explicit && value.Language.Equals(new(writer.XmlLang)))
        {
            // No need to write language
            await writer.WriteStringAsync(value.Value);
            return;
        }

        if(writer.WriteState == WriteState.Attribute)
        {
            // Write value first, then replace with the xml:lang attribute
            await writer.WriteStringAsync(value.Value);
            await writer.WriteEndAttributeAsync();

            await writer.WriteStartAttributeAsync("xml", "lang", XNamespace.Xml.NamespaceName);
            await writer.WriteStringAsync(value.Language.Value);
        }
        else
        {
            // Write xml:lang as the last attribute
            await writer.WriteAttributeStringAsync("xml", "lang", XNamespace.Xml.NamespaceName, value.Language.Value);
            await writer.WriteStringAsync(value.Value);
        }
    }

    async ValueTask IValueXmlEncoder<DateTime>.Encode(XmlWriter writer, DateTime value)
    {
        if(value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date must be in UTC.", nameof(value));
        }
        await writer.WriteStringAsync(XmlConvert.ToString(value, XmlDateTimeSerializationMode.Utc));
    }

    ValueTask IValueXmlEncoder<DateTimeOffset>.Encode(XmlWriter writer, DateTimeOffset value)
    {
        return new(writer.WriteStringAsync(XmlConvert.ToString(value)));
    }

    ValueTask IValueXmlEncoder<DateComponents>.Encode(XmlWriter writer, DateComponents value)
    {
        return new(writer.WriteStringAsync(value.ToString()));
    }

    ValueTask IValueXmlEncoder<TimeZoneOffset>.Encode(XmlWriter writer, TimeZoneOffset value)
    {
        return new(writer.WriteStringAsync(value.ToString()));
    }

    ValueTask IValueXmlEncoder<Uri>.Encode(XmlWriter writer, Uri value)
    {
        return new(writer.WriteStringAsync(value.OriginalString));
    }
}

public interface IValueXmlEncoder<in T>
{
    ValueTask Encode(XmlWriter writer, T value);
}
