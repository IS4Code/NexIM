using System;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xrd.Protocol;

public partial interface IUniversalHandler : IPayloadHandler
{

}

[ComplexType, Namespace(XrdNs)]
public interface IPropertyHandler : IPayloadHandler
{
    [Name("Property")]
    ValueTask Property([Name("type")] Uri? type, string? value);
}

[ComplexType, Namespace(XrdNs)]
public interface IResourceDescriptorHandler : IPropertyHandler
{
    [Name("Subject")]
    ValueTask Subject(Uri? identifier);

    [Name("Alias")]
    ValueTask Alias(Uri? identifier);

    [Name("Expires")]
    ValueTask Expires(DateTime? date);

    [Name("Link")]
    ValueTask<IResourceLinkHandler> Link([Name("rel")] Token<LinkRelation>? relation, [Name("type")] string? type, [Name("href")] Uri? href, [Name("template")] string? template);
}

[ComplexType, Namespace(XrdNs)]
public interface IResourceLinkHandler : IPropertyHandler
{
    [Name("Title")]
    ValueTask Title(LanguageTaggedString? text);
}

[SimpleType]
public enum LinkRelation
{
    [Name("urn:xmpp:alt-connections:websocket")]
    WebSocketConnection
}
