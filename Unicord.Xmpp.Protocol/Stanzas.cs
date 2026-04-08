using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;
using Unicord.Primitives.Xml.Handlers;

namespace Unicord.Xmpp.Protocol;

[SimpleType]
public enum StanzaKind
{
    [Name("message")] Message,
    [Name("presence")] Presence,
    [Name("iq")] InfoQuery
}

[SimpleType]
public enum StanzaType
{
    [Name("error")] Error,

    [Name("get")] Get,
    [Name("set")] Set,
    [Name("result")] Result,

    [Name("normal")] Normal,
    [Name("chat")] Chat,
    [Name("groupchat")] GroupChat,
    [Name("headline")] Headline,

    [Name("subscribe")] Subscribe,
    [Name("subscribed")] Subscribed,
    [Name("unsubscribe")] Unsubscribe,
    [Name("unsubscribed")] Unsubscribed,
    [Name("unavailable")] Unavailable,
    [Name("probe")] Probe
}

[StructLayout(LayoutKind.Auto)]
public record struct Stanza(
    Token<StanzaType>? Type,
    XmppResource? From = null,
    XmppResource? To = null,
    Token<StanzaIdentifier>? Identifier = null,
    LanguageCode? Language = null
);

[SimpleType]
public enum StanzaIdentifier
{

}

public partial interface IUniversalHandler : IPayloadHandler
{

}

[ComplexType]
public interface IStanzaHandler : IPayloadHandler
{
    [Name("error")]
    ValueTask<IStanzaErrorHandler> Error([Name("type")] Token<ErrorType>? type, [Name("code")] int? code, [Name("by")] XmppResource? by);
}
