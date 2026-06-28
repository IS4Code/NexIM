using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace NexIM.Primitives.Xml;

/// <summary>
/// Provides support for encoding to XML.
/// </summary>
public abstract class XmlEncoder :
    IValueXmlEncoder<TemporaryString>,
    IValueXmlEncoder<TemporaryUtf8String>,
    IValueXmlEncoder<Base64<ArraySegment<byte>>>,
    IValueXmlEncoder<Hex<ArraySegment<byte>>>,
    IValueXmlEncoder<Base64<TemporaryArray<byte>>>,
    IValueXmlEncoder<Hex<TemporaryArray<byte>>>,
    IValueXmlEncoder<Base64<TemporaryFile>>,
    IValueXmlEncoder<Hex<TemporaryFile>>,
    IValueXmlEncoder<IReadOnlyList<string>>,
    IValueXmlEncoder<Token<Enum>>,
    IValueXmlEncoder<LanguageCode>,
    IValueXmlEncoder<LanguageTaggedString>,
    IValueXmlEncoder<DateTime>,
    IValueXmlEncoder<DateTimeOffset>,
    IValueXmlEncoder<TimeSpan>,
    IValueXmlEncoder<DateComponents>,
    IValueXmlEncoder<TimeZoneOffset>,
    IValueXmlEncoder<ValueUri>,
    IValueXmlEncoder<MailAddress>,
    IValueXmlEncoder<True>
{
    protected abstract XmlWriter Writer { get; }
    protected virtual bool LowerCaseHex => false;

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

    static readonly TemporaryArray<byte>.AsynchronousWriter<XmlWriter> xmlTemporaryBase64Writer = static (buffer, writer) => {
        return new(writer.WriteBase64Async(buffer.Array!, buffer.Offset, buffer.Count));
    };

    static readonly TemporaryArray<byte>.AsynchronousWriter<(XmlWriter, bool)> xmlTemporaryHexWriter = static (buffer, info) => {
        var (writer, lowercase) = info;
        return new(
            lowercase
            ? writer.WriteLowerCaseBinHexAsync(buffer.Array!, buffer.Offset, buffer.Count)
            : writer.WriteBinHexAsync(buffer.Array!, buffer.Offset, buffer.Count)
        );
    };

    ValueTask IValueXmlEncoder<Base64<ArraySegment<byte>>>.Encode(XmlWriter writer, Base64<ArraySegment<byte>> value)
    {
        return xmlTemporaryBase64Writer(value, writer);
    }

    ValueTask IValueXmlEncoder<Hex<ArraySegment<byte>>>.Encode(XmlWriter writer, Hex<ArraySegment<byte>> value)
    {
        return xmlTemporaryHexWriter(value, (writer, LowerCaseHex));
    }

    ValueTask IValueXmlEncoder<TemporaryString>.Encode(XmlWriter writer, TemporaryString value)
    {
        return value.WriteToAsync(xmlTemporaryCharWriter, writer);
    }

    ValueTask IValueXmlEncoder<TemporaryUtf8String>.Encode(XmlWriter writer, TemporaryUtf8String value)
    {
        return value.WriteToAsync(xmlTemporaryBase64Writer, writer);
    }

    ValueTask IValueXmlEncoder<Base64<TemporaryArray<byte>>>.Encode(XmlWriter writer, Base64<TemporaryArray<byte>> value)
    {
        return value.Value.WriteToAsync(xmlTemporaryBase64Writer, writer);
    }

    ValueTask IValueXmlEncoder<Hex<TemporaryArray<byte>>>.Encode(XmlWriter writer, Hex<TemporaryArray<byte>> value)
    {
        return value.Value.WriteToAsync(xmlTemporaryHexWriter, (writer, LowerCaseHex));
    }

    ValueTask IValueXmlEncoder<Base64<TemporaryFile>>.Encode(XmlWriter writer, Base64<TemporaryFile> value)
    {
        return value.Value.WriteToAsync(xmlTemporaryBase64Writer, writer);
    }

    ValueTask IValueXmlEncoder<Hex<TemporaryFile>>.Encode(XmlWriter writer, Hex<TemporaryFile> value)
    {
        return value.Value.WriteToAsync(xmlTemporaryHexWriter, (writer, LowerCaseHex));
    }

    async ValueTask IValueXmlEncoder<IReadOnlyList<string>>.Encode(XmlWriter writer, IReadOnlyList<string> value)
    {
        bool first = true;
        foreach(var item in value)
        {
            if(first)
            {
                first = false;
            }
            else
            {
                await writer.WriteStringAsync(" ");
            }
            await writer.WriteStringAsync(item);
        }
    }

    protected async ValueTask EncodeTokenAsync(XmlWriter writer, string tokenValue)
    {
        await writer.WriteStringAsync(tokenValue);
    }

    ValueTask IValueXmlEncoder<Token<Enum>>.Encode(XmlWriter writer, Token<Enum> token)
    {
        return EncodeTokenAsync(writer, token.Value);
    }

    async ValueTask IValueXmlEncoder<LanguageCode>.Encode(XmlWriter writer, LanguageCode value)
    {
        await writer.WriteStringAsync(value.Value);
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

    ValueTask IValueXmlEncoder<TimeSpan>.Encode(XmlWriter writer, TimeSpan value)
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

    ValueTask IValueXmlEncoder<ValueUri>.Encode(XmlWriter writer, ValueUri value)
    {
        return new(writer.WriteStringAsync(value.ToString()));
    }

    ValueTask IValueXmlEncoder<MailAddress>.Encode(XmlWriter writer, MailAddress value)
    {
        return new(writer.WriteStringAsync(value.Address));
    }

    ValueTask IValueXmlEncoder<True>.Encode(XmlWriter writer, True value)
    {
        return new(writer.WriteStringAsync(value.ToString()));
    }
}

public interface IValueXmlEncoder<in T>
{
    ValueTask Encode(XmlWriter writer, T value);
}
