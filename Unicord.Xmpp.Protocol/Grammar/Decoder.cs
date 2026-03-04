using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;

namespace Unicord.Xmpp.Protocol.Grammar;

public partial class Decoder : XmlDecoder, IValueXmlDecoder<XmppResource>
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

    async ValueTask<XmppResource> IValueXmlDecoder<XmppResource>.Decode(XmlReader reader)
    {
        var token = await DecodeTokenAsync(reader);
        return XmppResource.Parse(token.AsMemory(), reader.NameTable);
    }
}
