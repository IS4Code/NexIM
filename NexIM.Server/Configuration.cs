using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using NexIM.Server.Net;

namespace NexIM.Server;

public class Configuration
{
    public static readonly bool PreserveUnavailableStatus = false;

    public static readonly TimeSpan XmppMinDelayTime = TimeSpan.FromSeconds(1);

    public static bool OnUnexpectedException(Exception e)
    {
        if(e is SocketException or WebSocketException or OperationCanceledException)
        {
            // Connection closed
            return true;
        }

        lock(typeof(Console))
        {
            Console.WriteLine(e);
        }
        Debugger.Break();
        return true;
    }

    public static IHttpListener CreateHttpListener()
    {
        return new ManagedHttpListener();
    }
}
