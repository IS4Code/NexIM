using System;
using System.Threading.Tasks;

namespace NexIM.Xmpp.Protocol.Handlers;

/// <summary>
/// Provides a handler with empty implementation of all handler methods.
/// </summary>
public partial class NullHandler : IAsyncDisposable, IXmppReceivingHandler, IXmppSendingHandler, IStreamHandler
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

    ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza) => new(this);
    ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza) => new(this);
    ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza) => new(this);

    ValueTask IXmppReceivingHandler.StreamStarted() => default;
    ValueTask IXmppReceivingHandler.StreamStopped() => default;

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return default;
    }
}
