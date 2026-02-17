using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Server.Primitives;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Protocol;

using static Constants;

static file class Constants
{
    public const string Client = "jabber:client";
    public const string IqRoster = "jabber:iq:roster";
    public const string IqAuth = "jabber:iq:auth";
    public const string ChatStates = "http://jabber.org/protocol/chatstates";
    public const string XmppTls = "urn:ietf:params:xml:ns:xmpp-tls";
}

[StructLayout(LayoutKind.Auto)]
public record struct Stanza(
    string? Type = null,
    XmppResource? From = null,
    XmppResource? To = null,
    string? Identifier = null
);

public interface IPayloadHandler : IAsyncDisposable
{
    ValueTask Other(XElement payload);
}

[ComplexType]
public interface IStreamHandler : IPayloadHandler
{
    [Name("features", "http://etherx.jabber.org/streams")]
    ValueTask<IFeaturesHandler> Features();
}

[ComplexType, Namespace(XmppTls)]
public interface IStreamTlsHandler : IStreamHandler
{
    [Name("starttls")]
    ValueTask StartTls();

    [Name("proceed")]
    ValueTask ProceedTls();

    [Name("failure")]
    ValueTask FailureTls();
}

public interface IStanzaHandler : IStreamTlsHandler
{
    ValueTask<IMessageHandler> Message(in Stanza stanza);
    ValueTask<IPresenceHandler> Presence(in Stanza stanza);
    ValueTask<IInfoQueryHandler> InfoQuery(in Stanza stanza);
}

[ComplexType]
public interface IFeaturesHandler : IPayloadHandler
{
    [Name("auth", "http://jabber.org/features/iq-auth")]
    ValueTask IqAuth();

    [Name("starttls", XmppTls)]
    ValueTask<ITlsFeaturesHandler> StartTls();
}

[ComplexType, Namespace(XmppTls)]
public interface ITlsFeaturesHandler : IPayloadHandler
{
    [Name("required")]
    ValueTask Required();
}

[ComplexType, Namespace(Client)]
public interface IMessageHandler : IPayloadHandler
{
    [Name("subject")]
    ValueTask Subject(string? text);

    [Name("body")]
    ValueTask Body(string? text);

    [Name("active", ChatStates)]
    ValueTask Active();

    [Name("inactive", ChatStates)]
    ValueTask Inactive();

    [Name("composing", ChatStates)]
    ValueTask Composing();

    [Name("paused", ChatStates)]
    ValueTask Paused();

    [Name("gone", ChatStates)]
    ValueTask Gone();
}

[ComplexType, Namespace(Client)]
public interface IPresenceHandler : IPayloadHandler
{
    [Name("show")]
    ValueTask Show(string? text);

    [Name("status")]
    ValueTask Status(string? text);

    [Name("priority")]
    ValueTask Priority(sbyte? value);

    [Name("delay", "urn:xmpp:delay")]
    ValueTask Delay([Name("stamp")] DateTimeOffset? stamp);
}

[ComplexType]
public interface IInfoQueryHandler : IPayloadHandler
{
    [Name("query", IqRoster)]
    ValueTask<IRosterQueryHandler> RosterQuery();

    [Name("query", IqAuth)]
    ValueTask<IAuthQueryHandler> AuthQuery();
}

[ComplexType, Namespace(IqRoster)]
public interface IRosterQueryHandler : IPayloadHandler
{
    [Name("item")]
    ValueTask Item(string? identifier);
}

[ComplexType, Namespace(IqAuth)]
public interface IAuthQueryHandler : IPayloadHandler
{
    [Name("username")]
    ValueTask Username(string? value);

    [Name("password")]
    ValueTask Password(TemporaryString? value);

    [Name("digest")]
    ValueTask Digest(string? value);

    [Name("resource")]
    ValueTask Resource(string? value);
}

public enum MessageType
{
    Normal,
    Chat,
    GroupChat,
    Headline,
    Error
}

public enum ErrorType
{
    Auth,
    Cancel,
    Continue,
    Modify,
    Wait
}

public enum InfoQueryType
{
    Get,
    Set,
    Result,
    Error
}

public abstract class PayloadHandler : IPayloadHandler
{
    public abstract ValueTask DisposeAsync();

    public virtual ValueTask Other(XElement payload)
    {
        return default;
    }
}
