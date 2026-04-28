using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using Json = NexIM.Primitives.Json.Grammar;
using JsonHandlers = NexIM.Primitives.Json.Handlers;
using Xml = NexIM.Primitives.Xml.Grammar;
using XmlHandlers = NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xrd.Protocol;

public partial interface IUniversalHandler : XmlHandlers.IPayloadHandler
{

}

[Xml.ComplexType, Xml.Namespace(XrdNs)]
[Json.ComplexType]
public interface IPropertyHandler : XmlHandlers.IPayloadHandler, JsonHandlers.IPayloadHandler
{
    [Xml.Name("Property")]
    [Json.Name("properties"), Json.ValueKind(Json.ValueKind.Object)]
    ValueTask Property([Xml.Name("type"), Json.Key] ValueUri? type, string? value);
}

[Xml.ComplexType, Xml.Namespace(XrdNs)]
[Json.ComplexType]
public interface IResourceDescriptorHandler : IPropertyHandler
{
    [Xml.Name("Subject")]
    [Json.Name("subject")]
    ValueTask Subject(ValueUri? identifier);

    [Xml.Name("Alias")]
    [Json.Name("aliases"), Json.ValueKind(Json.ValueKind.Array)]
    ValueTask Alias(ValueUri? identifier);

    [Xml.Name("Expires")]
    [Json.Name("expires")]
    ValueTask Expires(DateTime? date);

    [Xml.Name("Link")]
    [Json.Name("links"), Json.ValueKind(Json.ValueKind.Array)]
    ValueTask<IResourceLinkHandler> Link([Xml.Name("rel"), Json.Name("rel")] Token<LinkRelation>? relation, [Xml.Name("type"), Json.Name("type")] string? type, [Xml.Name("href"), Json.Name("href")] ValueUri? href, [Xml.Name("template"), Json.Name("template")] string? template);
}

[Xml.ComplexType, Xml.Namespace(XrdNs)]
[Json.ComplexType]
public interface IResourceLinkHandler : IPropertyHandler
{
    [Xml.Name("Title")]
    [Json.Name("titles")]
    ValueTask Title(LanguageTaggedString? text);
}

[Xml.SimpleType]
[Json.SimpleType]
public enum LinkRelation
{
    [Xml.Name(XmppWebSocket)]
    [Json.Name(XmppWebSocket)]
    WebSocketConnection
}
