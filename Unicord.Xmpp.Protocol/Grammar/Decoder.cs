using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xmpp.Protocol.Grammar;

public abstract partial class Decoder : XmlDecoder, IValueXmlDecoder<XmppAddress>, IValueXmlDecoder<XmppResource>
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
}

public class ClientDecoder : Decoder
{
    public static readonly string Namespace = Vocabulary.Standard.JabberClientNs.Value;

    public override string GetDefaultNamespace(XmlNameTable nameTable)
    {
        return nameTable switch {
            Vocabulary => Namespace,
            _ => nameTable.Add(Namespace)
        };
    }
}
