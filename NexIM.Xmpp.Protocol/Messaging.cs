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

    [Name("addresses", Constants.Addresses)]
    ValueTask<IAddressesHandler> Addresses();
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

    [Name("html", XHtmlIM)]
    ValueTask<IXHtmlHandler> Html();

    [Name("active", ChatStates)] ValueTask Active();
    [Name("inactive", ChatStates)] ValueTask Inactive();
    [Name("composing", ChatStates)] ValueTask Composing();
    [Name("paused", ChatStates)] ValueTask Paused();
    [Name("gone", ChatStates)] ValueTask Gone();

    [Name("no-permanent-store", XmppHints)] ValueTask NoPermanentStore();
    [Name("no-store", XmppHints)] ValueTask NoStore();
    [Name("no-copy", XmppHints)] ValueTask NoCopy();
    [Name("store", XmppHints)] ValueTask Store();

    [Name("request", XmppReceipts)]
    ValueTask ReceiptRequest();

    [Name("received", XmppReceipts)]
    ValueTask ReceiptResponse([Name("id")] string? id);

    [Name("markable", XmppChatMarkers)]
    ValueTask DisplayedRequest(LanguageTaggedString? text);

    [Name("displayed, XmppChatMarkers")]
    ValueTask DisplayedResponse([Name("id")] string? id, LanguageTaggedString? text);

    [Name("reply", XmppReceipts)]
    ValueTask ReplyTo([Name("id")] string? id, [Name("to")] XmppResource? to);

    [Name("referenced-stanza", XmppSid)]
    ValueTask Reference([Name("id")] string? id, [Name("by")] XmppResource? by);
}

[ComplexType, Namespace(Addresses)]
public interface IAddressesHandler : IPayloadHandler
{
    [Name("address")]
    ValueTask Address(
        [Name("type")] Token<AddressType>? type,
        [Name("jid")] XmppResource? address,
        [Name("node")] Token<DiscoNode>? node,
        [Name("uri")] ValueUri? uri,
        [Name("desc")] LanguageTaggedString? description,
        [Name("delivered")] True? delivered
    );
}

[SimpleType]
public enum AddressType
{
    [Name("bcc")] BlindCarbonCopy,
    [Name("cc")] CarbonCopy,
    [Name("noreply")] NoReply,
    [Name("replyroom")] ReplyRoom,
    [Name("replyto")] ReplyTo,
    [Name("to")] To,
    [Name("ofrom")] OriginalFrom
}
