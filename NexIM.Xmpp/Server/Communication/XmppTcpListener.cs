using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Server;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Listens to TCP XMPP connections.
/// </summary>
public class XmppTcpListener : XmppServerListener<TcpClient, XmppStreamSession>
{
    protected override bool PrettyOutput => true;

    protected override ConformanceLevel ConformanceLevel => ConformanceLevel.Document;

    new XmppServerReceiver Receiver => (XmppServerReceiver)base.Receiver;

    public ICollection<IPEndPoint> EndPoints { get; } = new HashSet<IPEndPoint>();

    public XmppTcpListener(XmppServerReceiver receiver) : base(receiver)
    {

    }

    public async override Task RunAsync(CancellationToken cancellationToken = default)
    {
        // One listener for every endpoint
        using TcpListeners listeners = new(EndPoints.Select(static ep => new TcpListener(ep)).ToArray(), cancellationToken);

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

    protected override ValueTask<XmppStreamSession> CreateSession(TcpClient client, CancellationToken cancellationToken)
    {
        return new(new XmppTcpSession(Receiver, client.GetStream(), ReaderSettings, WriterSettings, cancellationToken));
    }

    readonly struct TcpListeners(TcpListener[] listeners, bool[] running, Task<TcpClient>[] tasks, CancellationToken cancellationToken) : IDisposable
    {
        public TcpListeners(TcpListener[] listeners, CancellationToken cancellationToken) : this(listeners, new bool[listeners.Length], new Task<TcpClient>[listeners.Length], cancellationToken)
        {

        }

        public void Start()
        {
            for(int i = 0; i < listeners.Length; i++)
            {
                listeners[i].Start();
                running[i] = true;
            }
        }

        public async ValueTask<TcpClient> AcceptTcpClientAsync()
        {
            switch(listeners.Length)
            {
                case 0:
                    throw new InvalidOperationException("No TCP endpoints were configured.");
                case 1:
                    // Passthrough
                    return await listeners[0].AcceptTcpClientAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if(tasks[0] == null)
            {
                // First run, start accepting from all
                for(int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] ??= listeners[i].AcceptTcpClientAsync(cancellationToken).AsTask();
                }
            }

            var accepted = await Task.WhenAny(tasks);

            // Start listening on the endpoint again
            int acceptedIndex = Array.IndexOf(tasks, accepted);
            tasks[acceptedIndex] = listeners[acceptedIndex].AcceptTcpClientAsync(cancellationToken).AsTask();

            // There is currently no mechanism that would ensure fair selection for the endpoints

            return await accepted;
        }

        public void Dispose()
        {
            Dispose(0);
        }

        void Dispose(int start)
        {
            while(start < running.Length)
            {
                if(running[start])
                {
                    try
                    {
                        listeners[start].Stop();
                        running[start] = false;
                    }
                    finally
                    {
                        if(running[start])
                        {
                            // Exception during Stop, continue with the rest
                            Dispose(start + 1);
                        }
                    }
                }
                start++;
            }
        }
    }
}
