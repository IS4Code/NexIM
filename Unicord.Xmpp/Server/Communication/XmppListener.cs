using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

/// <summary>
/// Represents an entity capable of accepting XMPP connections.
/// </summary>
/// <typeparam name="TSession">
/// The type of accepted sessions.
/// </typeparam>
/// <param name="receiver">
/// The receiver object that provides handlers for incoming sessions.
/// </param>
public abstract class XmppListener<TSession>(IXmppReceiver<TSession> receiver) where TSession : IXmppSession
{
    protected IXmppReceiver<TSession> Receiver => receiver;

    public abstract Task RunAsync(CancellationToken cancellationToken = default);
}
