using System;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Provides support for encoding to XML.
/// </summary>
public abstract class XmlEncoder : IValueXmlEncoder<TemporaryString>, IValueXmlEncoder<ArraySegment<byte>>, IValueXmlEncoder<TemporaryArray<byte>>, IValueXmlEncoder<Token<Enum>>, IValueXmlEncoder<LanguageTaggedString>
{
    protected abstract XmlWriter Writer { get; }

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
        if(!writer.XmlLang.Equals(value.LanguageTag, StringComparison.OrdinalIgnoreCase))
        {
            if(writer.WriteState == WriteState.Attribute)
            {
                throw new NotSupportedException("A language-tagged string cannot be stored in an attribute.");
            }
            await writer.WriteAttributeStringAsync("xml", "lang", "http://www.w3.org/XML/1998/namespace", value.LanguageTag);
        }
        await writer.WriteStringAsync(value.Value);
    }
}

public interface IValueXmlEncoder<in T>
{
    ValueTask Encode(XmlWriter writer, T value);
}
