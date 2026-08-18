using NexIM.Server.Authentication;
using NexIM.Xmpp.Protocol;

namespace NexIM.Xmpp.Server.Handlers;

using static SaslStatus;

internal static class SaslExtensions
{
    public static XmppSaslException? ToXmppException(this SaslStatus reason)
    {
        return reason switch {
            Challenge or Success => null,
            AccountDisabled => XmppSaslException.AccountDisabled(),
            CredentialsExpired => XmppSaslException.CredentialsExpired(),
            TransportNotSecure => XmppSaslException.EncryptionRequired(),
            AuthorizationFailed => XmppSaslException.InvalidAuthzid(),
            MechanismNotSupported => XmppSaslException.InvalidMechanism(),
            InvalidRequest => XmppSaslException.MalformedRequest(),
            MechanismTooWeak => XmppSaslException.MechanismTooWeak(),
            AuthenticationFailed => XmppSaslException.NotAuthorized(),
            InternalError => XmppSaslException.TemporaryAuthFailure(),
            _ => XmppSaslException.TemporaryAuthFailure()
        };
    }
}
