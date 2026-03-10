using System;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;

namespace Unicord.Xmpp.Protocol;

[ComplexType]
public interface ISenderPresentation : IPayloadHandler
{
    [Name("nick", "http://jabber.org/protocol/nick")]
    ValueTask Nickname(string? text);
}

[ComplexType]
public interface IPresenceHandler : IStanzaHandler, ISenderPresentation
{
    [Name("show")]
    ValueTask Show(Token<StatusType>? text);

    [Name("status")]
    ValueTask Status(LanguageTaggedString? text);

    [Name("priority")]
    ValueTask Priority(sbyte? value);

    [Name("delay", "urn:xmpp:delay")]
    ValueTask Delay([Name("stamp")] DateTimeOffset? stamp);
}

[SimpleType]
public enum StatusType
{
    [Name("chat")] Chat,
    [Name("away")] Away,
    [Name("xa")] ExtendedAway,
    [Name("dnd")] DoNotDisturb
}
