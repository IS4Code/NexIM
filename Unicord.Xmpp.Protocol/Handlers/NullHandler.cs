using System;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol.Handlers;

/// <summary>
/// Provides a handler with empty implementation of all handler methods.
/// </summary>
public partial class NullHandler : IAsyncDisposable, IXmppReceivingHandler, IXmppSendingHandler
{
    public static readonly NullHandler Instance = new();

    string? IXmppSendingHandler.StreamIdentifier => null;
    XmppResource? IXmppSendingHandler.LocalResource => null;
    XmppResource? IXmppSendingHandler.RemoteResource { get => null; set { } }
    string? IXmppSendingHandler.LocalLanguage { get => null; set { } }
    string? IXmppSendingHandler.RemoteLanguage { get => null; set { } }
    string IXmppHandler.DefaultNamespace => String.Empty;

    protected NullHandler()
    {

    }

    ValueTask IXmppReceivingHandler.StreamStarted()
    {
        return default;
    }

    ValueTask IXmppReceivingHandler.StreamStopped()
    {
        return default;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return default;
    }
}
