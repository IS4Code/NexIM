using System;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol.Grammar;

public abstract partial class Decoder : XmlDecoder,
    IValueXmlDecoder<XmppAddress>,
    IValueXmlDecoder<XmppResource>,
    IValueXmlDecoder<Number>,
    IValueXmlDecoder<InlineStyle>
{
    public readonly record struct Result(bool Success, IPayloadHandler? InnerHandler);

    public partial ValueTask<Result> DecodePayload(XmlReader reader, IPayloadHandler handler);

    protected override void ThrowElementNotEmpty()
    {
        throw XmppStanzaException.BadRequest("Element was expected to be empty.");
    }

    protected override void ThrowElementNotSimple()
    {
        throw XmppStanzaException.BadRequest("Element was expected to have textual value.");
    }

    async ValueTask<XmppAddress> IValueXmlDecoder<XmppAddress>.Decode(XmlReader reader)
    {
        var token = await DecodeTokenAsync(reader);
        return XmppAddress.Parse(token.AsMemory(), reader.NameTable);
    }

    async ValueTask<XmppResource> IValueXmlDecoder<XmppResource>.Decode(XmlReader reader)
    {
        var token = await DecodeTokenAsync(reader);
        return XmppResource.Parse(token.AsMemory(), reader.NameTable);
    }

    async ValueTask<Number> IValueXmlDecoder<Number>.Decode(XmlReader reader)
    {
        var value = await reader.ReadContentAsStringAsync();
        return new(value);
    }

    async ValueTask<InlineStyle> IValueXmlDecoder<InlineStyle>.Decode(XmlReader reader)
    {
        var value = await reader.ReadContentAsStringAsync();
        return new(value);
    }
}

public class ClientDecoder : Decoder
{
    public static readonly string Namespace = Vocabulary.Standard.JabberClientNs.Value;

    public override string GetDefaultNamespace(XmlNameTable nameTable)
    {
        return nameTable.Add(Namespace);
    }
}
