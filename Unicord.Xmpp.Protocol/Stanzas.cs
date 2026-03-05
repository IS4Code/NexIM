using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;

namespace Unicord.Xmpp.Protocol;

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
    Token<StanzaType>? Type = null,
    XmppResource? From = null,
    XmppResource? To = null,
    string? Identifier = null
);

public interface IPayloadHandler : IAsyncDisposable
{
    ValueTask Other(XElement payload);
}

[ComplexType, Namespace(Client)]
public interface IStanzaHandler : IPayloadHandler
{
    [Name("error")]
    ValueTask<IStanzaErrorHandler> Error([Name("type")] Token<ErrorType>? type, [Name("code")] int? code);
}

public abstract class PayloadHandler : IPayloadHandler
{
    public abstract ValueTask DisposeAsync();

    public virtual ValueTask Other(XElement payload)
    {
        return default;
    }
}
