using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[SimpleType]
public enum ErrorType
{
    [Name("auth")] Auth,
    [Name("cancel")] Cancel,
    [Name("continue")] Continue,
    [Name("modify")] Modify,
    [Name("wait")] Wait
}

[ComplexType, Namespace(Streams)]
public interface IStreamErrorTextHandler : IPayloadHandler
{
    [Name("text")]
    ValueTask Text(LanguageTaggedString? text);
}

[ComplexType, Namespace(Streams)]
public interface IStreamErrorHandler : IStreamErrorTextHandler
{
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
public interface IStanzaErrorTextHandler : IPayloadHandler
{
    [Name("text")]
    ValueTask Text(LanguageTaggedString? text);
}

[ComplexType, Namespace(Stanzas)]
public interface IStanzaErrorHandler : IStanzaErrorTextHandler
{
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
    [Name("policy-violation")] ValueTask PolicyViolation();
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

[ComplexType, Namespace(Compression)]
public interface ICompressionFailureHandler : IStanzaErrorHandler
{
    [Name("unsupported-method")] ValueTask UnsupportedMethod();
    [Name("setup-failed")] ValueTask SetupFailed();
    [Name("processing-failed")] ValueTask ProcessingFailed();
}

[ComplexType, Namespace(XmppSasl)]
public interface ISaslFailureHandler : IPayloadHandler
{
    [Name("aborted")] ValueTask Aborted();
    [Name("incorrect-encoding")] ValueTask IncorrectEncoding();
    [Name("invalid-authzid")] ValueTask InvalidAuthzid();
    [Name("invalid-mechanism")] ValueTask InvalidMechanism();
    [Name("mechanism-too-weak")] ValueTask MechanismTooWeak();
    [Name("not-authorized")] ValueTask NotAuthorized();
    [Name("temporary-auth-failure")] ValueTask TemporaryAuthFailure();
}
