using System.Threading.Tasks;
using Unicord.Xmpp.Protocol.Handlers;

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
    string DefaultNamespace { get; }
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
public abstract class XmppSendingHandler : BaseDelegatingStreamHandler<IStreamHandler, EmptyDisposable, XmppSendingHandler>, IXmppSendingHandler, IPayloadHandlerContext
{
    public string? StreamIdentifier { get; set; }
    public XmppResource? LocalResource { get; set; }
    public XmppResource? RemoteResource { get; set; }

    public abstract string DefaultLanguage { get; }
    public abstract string DefaultNamespace { get; }

    public string? LocalLanguage { get; set; }
    public string? RemoteLanguage { get; set; }

    protected sealed override EmptyDisposable Disposable => default;
}
