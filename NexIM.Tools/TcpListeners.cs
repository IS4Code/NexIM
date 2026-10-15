using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NexIM.Tools;

/// <summary>
/// Manages a collection of <see cref="TcpListener"/> instances on parallel endpoints.
/// </summary>
public readonly struct TcpListeners : IDisposable
{
    readonly TcpListener[] listeners;
    readonly bool[] running;
    readonly Task<TcpClient>[] tasks;
    readonly CancellationToken cancellationToken;

    private TcpListeners(TcpListener[] listeners, bool[] running, Task<TcpClient>[] tasks, CancellationToken cancellationToken)
    {
        this.listeners = listeners;
        this.running = running;
        this.tasks = tasks;
        this.cancellationToken = cancellationToken;
    }

    public TcpListeners(TcpListener[] listeners, CancellationToken cancellationToken) : this(listeners, new bool[listeners.Length], new Task<TcpClient>[listeners.Length], cancellationToken)
    {

    }

    public TcpListeners(IEnumerable<IPEndPoint> endPoints, CancellationToken cancellationToken) : this(endPoints.Select(static ep => new TcpListener(ep)).ToArray(), cancellationToken)
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
