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
    public const string Xml = "http://www.w3.org/XML/1998/namespace";
    public const string Client = "jabber:client";
    public const string IqRoster = "jabber:iq:roster";
    public const string IqAuth = "jabber:iq:auth";
    public const string ChatStates = "http://jabber.org/protocol/chatstates";
    public const string XmppTls = "urn:ietf:params:xml:ns:xmpp-tls";
    public const string Streams = "http://etherx.jabber.org/streams";
    public const string Stanzas = "urn:ietf:params:xml:ns:xmpp-stanzas";
    public const string FeaturesCompress = "http://jabber.org/features/compress";
    public const string Compression = "http://jabber.org/protocol/compress";
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

[ComplexType, Namespace(Streams)]
public interface ITransportHandler : IPayloadHandler
{
    [Name("features")]
    ValueTask<IFeaturesHandler> Features();

    [Name("error")]
    ValueTask<IStreamErrorHandler> Error();

    [Name("starttls", XmppTls)]
    ValueTask StartTls();

    [Name("proceed", XmppTls)]
    ValueTask ProceedTls();

    [Name("failure", XmppTls)]
    ValueTask FailureTls();

    [Name("compress", Compression)]
    ValueTask<ICompressionHandler> Compress();

    [Name("failure", Compression)]
    ValueTask<ICompressionFailureHandler> CompressionFailure();

    [Name("compressed", Compression)]
    ValueTask Compressed();
}

public interface IStreamHandler : ITransportHandler
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

    [Name("compression", FeaturesCompress)]
    ValueTask<ICompressionFeaturesHandler> Compression();
}

[ComplexType, Namespace(XmppTls)]
public interface ITlsFeaturesHandler : IPayloadHandler
{
    [Name("required")]
    ValueTask Required();
}

[ComplexType, Namespace(FeaturesCompress)]
public interface ICompressionFeaturesHandler : IPayloadHandler
{
    [Name("method")]
    ValueTask Method(string? name);
}

[ComplexType, Namespace(Compression)]
public interface ICompressionHandler : IPayloadHandler
{
    [Name("method")]
    ValueTask Method(string? name);
}

[ComplexType, Namespace(Compression)]
public interface ICompressionFailureHandler : IPayloadHandler, IStanzaErrorHandler
{
    [Name("unsupported-method")]
    ValueTask UnsupportedMethod();

    [Name("setup-failed")]
    ValueTask SetupFailed();

    [Name("processing-failed")]
    ValueTask ProcessingFailed();
}

[ComplexType, Namespace(Client)]
public interface IStanzaHandler : IPayloadHandler
{
    [Name("error")]
    ValueTask<IStanzaErrorHandler> Error([Name("type")] string? type);
}

[ComplexType, Namespace(Client)]
public interface IMessageHandler : IStanzaHandler
{
    [Name("subject")]
    ValueTask Subject(string? text);

    [Name("body")]
    ValueTask Body(string? text);

    [Name("active", ChatStates)] ValueTask Active();
    [Name("inactive", ChatStates)] ValueTask Inactive();
    [Name("composing", ChatStates)] ValueTask Composing();
    [Name("paused", ChatStates)] ValueTask Paused();
    [Name("gone", ChatStates)] ValueTask Gone();
}

[ComplexType, Namespace(Client)]
public interface IPresenceHandler : IStanzaHandler
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
public interface IInfoQueryHandler : IStanzaHandler
{
    [Name("query", IqRoster)]
    ValueTask<IRosterQueryHandler> RosterQuery([Name("ver")] string? version);

    [Name("query", IqAuth)]
    ValueTask<IAuthQueryHandler> AuthQuery();
}

[ComplexType, Namespace(IqRoster)]
public interface IRosterQueryHandler : IPayloadHandler
{
    [Name("item")]
    ValueTask<IRosterItemHandler> Item([Name("jid")] XmppAddress? identifier, [Name("name")] string? name, [Name("subscription")] string? subscription);
}

[ComplexType, Namespace(IqRoster)]
public interface IRosterItemHandler : IPayloadHandler
{
    [Name("group")]
    ValueTask Group(string? name);
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

[ComplexType, Namespace(Streams)]
public interface IStreamErrorHandler : IPayloadHandler
{
    [Name("text")]
    ValueTask Text(string? text, [Name("lang", Xml)] string? language);

    [Name("bad-format")] ValueTask BadFormat();
    [Name("bad-namespace-prefix")] ValueTask BadNamespacePrefix();
    [Name("conflict")] ValueTask Conflict();
    [Name("connection-timeout")] ValueTask ConnectionTimeout();
    [Name("host-gone")] ValueTask HostGone();
    [Name("host-unknown")] ValueTask HostUnknown();
    [Name("improper-addressing")] ValueTask ImproperAddressing();
    [Name("internal-server-error")] ValueTask InternalServerError();
    [Name("invalid-from")] ValueTask InvalidFrom();
    [Name("invalid-id")] ValueTask InvalidId();
    [Name("invalid-namespace")] ValueTask InvalidNamespace();
    [Name("invalid-xml")] ValueTask InvalidXml();
    [Name("not-authorized")] ValueTask NotAuthorized();
    [Name("policy-violation")] ValueTask PolicyViolation();
    [Name("remote-connection-failed")] ValueTask RemoteConnectionFailed();
    [Name("resource-constraint")] ValueTask ResourceConstraint();
    [Name("restricted-xml")] ValueTask RestrictedXml();
    [Name("see-other-host")] ValueTask SeeOtherHost(string? host);
    [Name("system-shutdown")] ValueTask SystemShutdown();
    [Name("undefined-condition")] ValueTask UndefinedCondition();
    [Name("unsupported-encoding")] ValueTask UnsupportedEncoding();
    [Name("unsupported-stanza-type")] ValueTask UnsupportedStanzaType();
    [Name("unsupported-version")] ValueTask UnsupportedVersion();
    [Name("xml-not-well-formed")] ValueTask XmlNotWellFormed();
}

[ComplexType, Namespace(Stanzas)]
public interface IStanzaErrorHandler : IPayloadHandler
{
    [Name("text")]
    ValueTask Text(string? text, [Name("lang", Xml)] string? language);

    [Name("bad-request")] ValueTask BadRequest();
    [Name("conflict")] ValueTask Conflict();
    [Name("feature-not-implemented")] ValueTask FeatureNotImplemented();
    [Name("forbidden")] ValueTask Forbidden();
    [Name("gone")] ValueTask Gone(string? newAddress);
    [Name("internal-server-error")] ValueTask InternalServerError();
    [Name("item-not-found")] ValueTask ItemNotFound();
    [Name("jid-malformed")] ValueTask JidMalformed();
    [Name("not-acceptable")] ValueTask NotAcceptable();
    [Name("not-allowed")] ValueTask NotAllowed();
    [Name("not-authorized")] ValueTask NotAuthorized();
    [Name("payment-required")] ValueTask PaymentRequired();
    [Name("recipient-unavailable")] ValueTask RecipientUnavailable();
    [Name("redirect")] ValueTask Redirect(string? alternateAddress);
    [Name("registration-required")] ValueTask RegistrationRequired();
    [Name("remote-server-not-found")] ValueTask RemoteServerNotFound();
    [Name("remote-server-timeout")] ValueTask RemoteServerTimeout();
    [Name("resource-constraint")] ValueTask ResourceConstraint();
    [Name("service-unavailable")] ValueTask ServiceUnavailable();
    [Name("subscription-required")] ValueTask SubscriptionRequired();
    [Name("undefined-condition")] ValueTask UndefinedCondition();
    [Name("unexpected-request")] ValueTask UnexpectedRequest();
}

public enum StanzaType
{
    Message,
    Presence,
    InfoQuery
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
