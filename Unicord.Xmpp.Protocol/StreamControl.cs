using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;

namespace Unicord.Xmpp.Protocol;

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
    [Name("EXTERNAL")] External,
    [Name("PLAIN")] Plain
}
