using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public abstract class XmppServerListener<TConnection, TSession> : XmppXmlListener<TSession> where TSession : XmppHandlerSession
{
    public XmppServerListener(IXmppReceiver<TSession> receiver) : base(receiver, ConformanceLevel.Document)
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
