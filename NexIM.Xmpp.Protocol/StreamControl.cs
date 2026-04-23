using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType, Namespace(Streams)]
public interface ITransportHandler : IPayloadHandler
{
    [Name("features")]
    ValueTask<IFeaturesHandler> Features();

    [Name("error")]
    ValueTask<IStreamErrorHandler> Error();

    [Name("starttls", XmppTls)]
    ValueTask TlsStart();

    [Name("proceed", XmppTls)]
    ValueTask TlsProceed();

    [Name("failure", XmppTls)]
    ValueTask TlsFailure();

    [Name("compress", Compression)]
    ValueTask<ICompressionHandler> Compress();

    [Name("failure", Compression)]
    ValueTask<ICompressionFailureHandler> CompressionFailure();

    [Name("compressed", Compression)]
    ValueTask Compressed();

    [Name("auth", XmppSasl)]
    ValueTask SaslAuth([Name("mechanism")] Token<SaslMechanism>? mechanism, TemporaryUtf8String? data);

    [Name("challenge", XmppSasl)]
    ValueTask SaslChallenge(TemporaryUtf8String? data);

    [Name("response", XmppSasl)]
    ValueTask SaslResponse(TemporaryUtf8String? data);

    [Name("abort", XmppSasl)]
    ValueTask SaslAbort();

    [Name("failure", XmppSasl)]
    ValueTask<ISaslFailureHandler> SaslFailure();

    [Name("success", XmppSasl)]
    ValueTask SaslSuccess();
}

public interface IStreamHandler : ITransportHandler
{
    ValueTask<IMessageHandler> Message(in Stanza stanza);
    ValueTask<IPresenceHandler> Presence(in Stanza stanza);
    ValueTask<IInfoQueryHandler> InfoQuery(in Stanza stanza);
}

[ComplexType, Namespace(Compression)]
public interface ICompressionHandler : IPayloadHandler
{
    [Name("method")]
    ValueTask Method(Token<CompressionMethod>? name);
}

[SimpleType]
public enum CompressionMethod
{
    [Name("zlib")] ZLib
}

[ComplexType, Namespace(XmppSasl)]
public interface ISaslMechanismsHandler : IPayloadHandler
{
    [Name("mechanism")]
    ValueTask Mechanism(Token<SaslMechanism>? name);
}

[SimpleType]
public enum SaslMechanism
{
    [Name("ANONYMOUS")] Anonymous,
    [Name("DIGEST-MD5")] DigestMd5,
    [Name("EAP-AES128")] EapAes128,
    [Name("EAP-AES128-PLUS")] EapAes128Plus,
    [Name("EXTERNAL")] External,
    [Name("GS2-KRB5")] Gs2Krb5,
    [Name("GS2-KRB5-PLUS")] Gs2Krb5Plus,
    [Name("GSSAPI")] GssApi,
    [Name("KERBEROS_V4")] KerberosV4,
    [Name("KERBEROS_V5")] KerberosV5,
    [Name("LOGIN")] Login,
    [Name("OAUTH10A")] OAuth10A,
    [Name("OAUTHBEARER")] OAuthBearer,
    [Name("OPENID20")] OpenId20,
    [Name("OTP")] Otp,
    [Name("PLAIN")] Plain,
    [Name("SAML20")] Saml20,
    [Name("SECURID")] Securid,
    [Name("SKEY")] Skey,
    [Name("SXOVER-PLUS")] SxoverPlus
}
