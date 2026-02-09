using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

using static XmppVocabulary;

internal static class XmppDecoder
{
    public readonly record struct Result(bool Success, IPayloadHandler? InnerHandler);

    public static async ValueTask<Result> DecodePayload(XmlReader reader, IPayloadHandler handler)
    {
        var elementName = reader.Name;
        var elementNs = reader.NamespaceURI;

        switch(elementName[0])
        {
            case 'q':
                if(elementName == Query)
                {
                    if(elementNs == JabberIqRosterNs)
                    {
                        return new(true, await Get<IInfoQueryHandler>().RosterQuery());
                    }
                    else if(elementNs == JabberIqAuthNs)
                    {
                        return new(true, await Get<IInfoQueryHandler>().AuthQuery());
                    }
                }
                break;
            case 'u':
                if(elementName == Username && elementNs == JabberIqAuthNs)
                {
                    var value = await ReadElementTextAsync(reader);
                    await Get<IAuthQueryHandler>().Username(value);
                    return new(true, null);
                }
                break;
            case 'p':
                if(elementName == Password && elementNs == JabberIqAuthNs)
                {
                    var value = await ReadElementTextAsync(reader);
                    await Get<IAuthQueryHandler>().Password(value);
                    return new(true, null);
                }
                break;
            case 'd':
                if(elementName == Digest && elementNs == JabberIqAuthNs)
                {
                    var value = await ReadElementTextAsync(reader);
                    await Get<IAuthQueryHandler>().Digest(value);
                    return new(true, null);
                }
                break;
            case 'r':
                if(elementName == Resource && elementNs == JabberIqAuthNs)
                {
                    var value = await ReadElementTextAsync(reader);
                    await Get<IAuthQueryHandler>().Resource(value);
                    return new(true, null);
                }
                break;
        }

        return new(false, null);

        THandler Get<THandler>() where THandler : IPayloadHandler
        {
            if(handler is not THandler typedHandler)
            {
                throw new NotSupportedException("The current payload handler does not support this element.");
            }
            return typedHandler;
        }
    }

    private static async ValueTask<string> ReadElementTextAsync(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            // Known to be empty
            await reader.ReadAsync();
            return "";
        }

        var sb = new StringBuilder();
        while(await reader.ReadAsync())
        {
            switch(reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                    // Append textual value
                    sb.Append(await reader.GetValueAsync());
                    break;
                case XmlNodeType.Element:
                    throw new XmppException("Non-textual element content not expected.", true);
                case XmlNodeType.EndElement:
                    return sb.ToString();
            }
        }

        return sb.ToString();
    }
}
