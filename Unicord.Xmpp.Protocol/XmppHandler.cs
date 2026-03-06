using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;

namespace Unicord.Xmpp.Protocol;

/// <summary>
/// Represents an entity capable of accepting incoming
/// XMPP connections.
/// </summary>
public interface IXmppReceiver<in THandler> where THandler : IXmppSendingHandler
{
    ValueTask<IXmppReceivingHandler> Connected(THandler session);
}

/// <summary>
/// Represents an XMPP stream.
/// </summary>
public interface IXmppHandler : IStreamHandler
{

}

/// <summary>
/// Represents an incoming XMPP stream.
/// </summary>
public interface IXmppReceivingHandler : IXmppHandler
{
    ValueTask StreamStarted();
    ValueTask StreamStopped();
}

/// <summary>
/// Represents an outgoing XMPP stream.
/// </summary>
public interface IXmppSendingHandler : IXmppHandler
{
    string? StreamIdentifier { get; }
    XmppResource? LocalResource { get; }
    XmppResource? RemoteResource { get; set; }
    string? LocalLanguage { get; set; }
    string? RemoteLanguage { get; set; }
}

/// <summary>
/// Provides a basic implementation of <see cref="IXmppSendingHandler"/>.
/// </summary>
public abstract class XmppSendingHandler : IXmppSendingHandler
{
    public string? StreamIdentifier { get; set; }
    public XmppResource? LocalResource { get; set; }
    public XmppResource? RemoteResource { get; set; }

    public abstract string DefaultLanguage { get; }

    public string? LocalLanguage { get; set; }
    public string? RemoteLanguage { get; set; }

    public abstract ValueTask DisposeAsync();

    protected abstract ValueTask<IMessageHandler> OnMessage(in Stanza stanza);
    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza)
    {
        return OnMessage(in stanza);
    }

    protected abstract ValueTask<IPresenceHandler> OnPresence(in Stanza stanza);
    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza)
    {
        return OnPresence(in stanza);
    }

    protected abstract ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza);
    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza)
    {
        return OnInfoQuery(in stanza);
    }

    protected abstract ValueTask<IFeaturesHandler> OnFeatures();
    ValueTask<IFeaturesHandler> ITransportHandler.Features()
    {
        return OnFeatures();
    }

    protected abstract ValueTask<IStreamErrorHandler> OnError();
    ValueTask<IStreamErrorHandler> ITransportHandler.Error()
    {
        return OnError();
    }

    protected abstract ValueTask OnTlsStart();
    ValueTask ITransportHandler.TlsStart()
    {
        return OnTlsStart();
    }

    protected abstract ValueTask OnTlsProceed();
    ValueTask ITransportHandler.TlsProceed()
    {
        return OnTlsProceed();
    }

    protected abstract ValueTask OnTlsFailure();
    ValueTask ITransportHandler.TlsFailure()
    {
        return OnTlsFailure();
    }

    protected abstract ValueTask OnOther(XmlReader payloadReader);
    ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        return OnOther(payloadReader);
    }

    protected abstract ValueTask<ICompressionHandler> OnCompress();
    ValueTask<ICompressionHandler> ITransportHandler.Compress()
    {
        return OnCompress();
    }

    protected abstract ValueTask<ICompressionFailureHandler> OnCompressionFailure();
    ValueTask<ICompressionFailureHandler> ITransportHandler.CompressionFailure()
    {
        return OnCompressionFailure();
    }

    protected abstract ValueTask OnCompressed();
    ValueTask ITransportHandler.Compressed()
    {
        return OnCompressed();
    }

    protected abstract ValueTask OnSaslAuth(Token<SaslMechanism>? mechanism, TemporaryUtf8String? data);
    ValueTask ITransportHandler.SaslAuth(Token<SaslMechanism>? mechanism, TemporaryUtf8String? data)
    {
        return OnSaslAuth(mechanism, data);
    }

    protected abstract ValueTask OnSaslChallenge(TemporaryUtf8String? data);
    ValueTask ITransportHandler.SaslChallenge(TemporaryUtf8String? data)
    {
        return OnSaslChallenge(data);
    }

    protected abstract ValueTask OnSaslResponse(TemporaryUtf8String? data);
    ValueTask ITransportHandler.SaslResponse(TemporaryUtf8String? data)
    {
        return OnSaslResponse(data);
    }

    protected abstract ValueTask OnSaslAbort();
    ValueTask ITransportHandler.SaslAbort()
    {
        return OnSaslAbort();
    }

    protected abstract ValueTask<ISaslFailureHandler> OnSaslFailure();
    ValueTask<ISaslFailureHandler> ITransportHandler.SaslFailure()
    {
        return OnSaslFailure();
    }

    protected abstract ValueTask OnSaslSuccess();
    ValueTask ITransportHandler.SaslSuccess()
    {
        return OnSaslSuccess();
    }
}
