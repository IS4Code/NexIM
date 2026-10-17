using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Server;
using NexIM.Tools;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Listens to TCP IRC connections.
/// </summary>
public class IrcTcpListener(IrcServerReceiver receiver)
{
    public ICollection<IPEndPoint> EndPoints { get; } = new HashSet<IPEndPoint>();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // One listener for every endpoint
        using TcpListeners listeners = new(EndPoints, cancellationToken);

        listeners.Start();

        while(await listeners.AcceptTcpClientAsync() is { } client)
        {
            HandleClient(client, cancellationToken);
        }
    }

    protected async void HandleClient(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            await Start(client, cancellationToken);
        }
        catch(Exception e) when(Configuration.OnUnexpectedException(e))
        {

        }
        finally
        {
            client.Dispose();
        }
    }

    protected async ValueTask Start(TcpClient client, CancellationToken cancellationToken)
    {
        // Initialize outgoing session
        await using var session = await CreateSession(client, cancellationToken);

        // Receive the session and prepare handler for incoming commands
        var handler = await receiver.Connected(session);

        await session.Run(handler, cancellationToken);
    }

    protected ValueTask<IrcStreamSession> CreateSession(TcpClient client, CancellationToken cancellationToken)
    {
        return new(new IrcTcpSession(receiver, client, cancellationToken));
    }
}
