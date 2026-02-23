using System.Threading.Tasks;
using System.Xml;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

public partial class XmppDecoder : XmlDecoder, IValueXmlDecoder<XmppResource>
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
        return XmppResource.Parse(await reader.ReadContentAsStringAsync());
    }
}
