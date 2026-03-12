using System;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Grammar;

namespace Unicord.Xmpp.Protocol;

[ComplexType]
public interface IDeliveryHandler : IPayloadHandler
{
    [Name("delay", "urn:xmpp:delay")]
    ValueTask Delay([Name("stamp")] DateTimeOffset? stamp, [Name("from")] XmppResource? from, LanguageTaggedString? reason);
}

[ComplexType]
public interface IMessageHandler : IStanzaHandler, IPresentationHandler, IDeliveryHandler
{
    [Name("subject")]
    ValueTask Subject(LanguageTaggedString? text);

    [Name("body")]
    ValueTask Body(LanguageTaggedString? text);

    [Name("active", ChatStates)] ValueTask Active();
    [Name("inactive", ChatStates)] ValueTask Inactive();
    [Name("composing", ChatStates)] ValueTask Composing();
    [Name("paused", ChatStates)] ValueTask Paused();
    [Name("gone", ChatStates)] ValueTask Gone();
}
