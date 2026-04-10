using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Grammar;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xmpp.Protocol;

[ComplexType]
public interface IPresentationHandler : IPayloadHandler
{
    [Name("nick", "http://jabber.org/protocol/nick")]
    ValueTask Nickname(string? text);
}

[ComplexType, Namespace(Caps)]
public interface ICapabilitiesHandler : IPayloadHandler
{
    [Name("c")]
    ValueTask Capabilities(
        [Name("hash")] Token<CapabilitiesHash>? hash,
        [Name("node")] string? node,
        [Name("ver")] string? version,
        [Name("ext")] string? extension
    );
}

[ComplexType]
public interface IPresenceHandler : IStanzaHandler, IPresentationHandler, IDeliveryHandler, ICapabilitiesHandler
{
    [Name("show")]
    ValueTask Show(Token<StatusType>? text);

    [Name("status")]
    ValueTask Status(LanguageTaggedString? text);

    [Name("priority")]
    ValueTask Priority(sbyte? value);
}

[SimpleType]
public enum StatusType
{
    [Name("chat")] Chat,
    [Name("away")] Away,
    [Name("xa")] ExtendedAway,
    [Name("dnd")] DoNotDisturb
}

[SimpleType]
public enum CapabilitiesHash
{
    [Name("sha-1")] Sha1
}
