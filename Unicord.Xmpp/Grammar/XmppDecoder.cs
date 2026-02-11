using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

internal static partial class XmppDecoder
{
    public readonly record struct Result(bool Success, IPayloadHandler? InnerHandler);

    public static partial ValueTask<Result> DecodePayload(XmlReader reader, IPayloadHandler handler);

    private static partial async ValueTask EmptyElementTextAsync(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            return;
        }

        await reader.ReadAsync();
        if(reader.NodeType != XmlNodeType.EndElement)
        {
            throw new XmppException("Element was expected to be empty.", false);
        }
    }

    private static partial async ValueTask<string> ReadElementTextAsync(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            // Known to be empty
            return "";
        }

        await reader.ReadAsync();
        switch(reader.NodeType)
        {
            case XmlNodeType.EndElement:
                return "";
            case XmlNodeType.Element:
                throw new XmppException("Element was expected to have textual value.", false);
        }

        return await reader.ReadContentAsStringAsync();
    }
}
