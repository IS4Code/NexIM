using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType]
public interface IDeliveryHandler : IPayloadHandler
{
    [Name("delay", XmppDelay)]
    ValueTask Delay([Name("stamp")] DateTime? timestamp, [Name("from")] XmppResource? from, LanguageTaggedString? reason);
}

[ComplexType]
public interface IMessageHandler : IStanzaHandler, IPresentationHandler, IDeliveryHandler
{
    [Name("subject")]
    ValueTask Subject(LanguageTaggedString? text);

    [Name("body")]
    ValueTask Body(LanguageTaggedString? text);

    [Name("thread")]
    ValueTask Thread(string? identifier, [Name("parent")] string? parent);

    [Name("active", ChatStates)] ValueTask Active();
    [Name("inactive", ChatStates)] ValueTask Inactive();
    [Name("composing", ChatStates)] ValueTask Composing();
    [Name("paused", ChatStates)] ValueTask Paused();
    [Name("gone", ChatStates)] ValueTask Gone();
}
