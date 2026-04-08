using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

/// <summary>
/// Represents an entity capable of creating XMPP sessions
/// representing accepted incoming connections.
/// </summary>
/// <typeparam name="TConnection">
/// The type of the connections.
/// </typeparam>
/// <typeparam name="TSession">
/// The type of the created sessions.
/// </typeparam>
public abstract class XmppServerListener<TConnection, TSession> : XmppXmlListener<TSession> where TSession : XmppHandlerSession
{
    public XmppServerListener(IXmppReceiver<TSession> receiver) : base(receiver)
    {

    }

    protected abstract ValueTask<TSession> CreateSession(TConnection connection, CancellationToken cancellationToken);

    protected async ValueTask Start(TConnection connection, CancellationToken cancellationToken)
    {
        // Initialize outgoing session
        await using var session = await CreateSession(connection, cancellationToken);

        // Receive the session and prepare handler for incoming commands
        await using var handler = await Receiver.Connected(session);

        await session.Run(handler, cancellationToken);
    }
}
