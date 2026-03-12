using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;

namespace Unicord.Xmpp.Protocol;

[ComplexType]
public interface IPresentationHandler : IPayloadHandler
{
    [Name("nick", "http://jabber.org/protocol/nick")]
    ValueTask Nickname(string? text);
}

[ComplexType]
public interface IPresenceHandler : IStanzaHandler, IPresentationHandler, IDeliveryHandler
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
