using System.Threading.Tasks;
using System.Xml.Linq;

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
public interface IXmppHandler : IStanzaHandler
{

}

/// <summary>
/// Represents an incoming XMPP stream.
/// </summary>
public interface IXmppReceivingHandler : IXmppHandler
{
    ValueTask StreamStarted();
}

/// <summary>
/// Represents an outgoing XMPP stream.
/// </summary>
public interface IXmppSendingHandler : IXmppHandler
{
    string? StreamIdentifier { get; }
    XmppResource? LocalResource { get; }
    XmppResource? RemoteResource { get; set; }
    string Language { get; set; }
}

/// <summary>
/// Provides a basic implementation of <see cref="IXmppSendingHandler"/>.
/// </summary>
public abstract class XmppSendingHandler : IXmppSendingHandler
{
    public string? StreamIdentifier { get; set; }
    public XmppResource? LocalResource { get; set; }
    public XmppResource? RemoteResource { get; set; }

    protected abstract string DefaultLanguage { get; }

    string? language;
    public string Language {
        get => language ?? DefaultLanguage;
        set => language = value;
    }

    public abstract ValueTask DisposeAsync();

    protected abstract ValueTask<IMessageHandler> OnMessage(in Stanza stanza);
    ValueTask<IMessageHandler> IStanzaHandler.Message(in Stanza stanza)
    {
        return OnMessage(in stanza);
    }

    protected abstract ValueTask<IPresenceHandler> OnPresence(in Stanza stanza);
    ValueTask<IPresenceHandler> IStanzaHandler.Presence(in Stanza stanza)
    {
        return OnPresence(in stanza);
    }

    protected abstract ValueTask<IInfoQueryHandler> OnInfoQuery(in Stanza stanza);
    ValueTask<IInfoQueryHandler> IStanzaHandler.InfoQuery(in Stanza stanza)
    {
        return OnInfoQuery(in stanza);
    }

    protected abstract ValueTask<IFeaturesHandler> OnFeatures();
    ValueTask<IFeaturesHandler> IStreamHandler.Features()
    {
        return OnFeatures();
    }

    protected abstract ValueTask OnStartTls();
    ValueTask IStreamTlsHandler.StartTls()
    {
        return OnStartTls();
    }

    protected abstract ValueTask OnProceedTls();
    ValueTask IStreamTlsHandler.ProceedTls()
    {
        return OnProceedTls();
    }

    protected abstract ValueTask OnFailureTls();
    ValueTask IStreamTlsHandler.FailureTls()
    {
        return OnFailureTls();
    }

    protected abstract ValueTask OnOther(XElement payload);
    ValueTask IPayloadHandler.Other(XElement payload)
    {
        return OnOther(payload);
    }
}
