using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using NexIM.Server.Net;

namespace NexIM.Server;

public class Configuration
{
    public static bool HttpListenerIsManaged { get; set; }

    public static readonly bool PreserveUnavailableStatus = false;

    public static readonly TimeSpan XmppMinDelayTime = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan PresenceCacheStaleDelay = TimeSpan.FromHours(1);

    public static readonly TimeSpan PresenceCacheInvalidateAfterProbeDelay = TimeSpan.FromMinutes(1);

    static Configuration()
    {
        // Used by SpaceWizards.HttpListener
#pragma warning disable SYSLIB0014
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
#pragma warning restore SYSLIB0014
    }

    public static bool OnUnexpectedException(Exception e)
    {
        if(e is SocketException or WebSocketException or SpaceWizards.HttpListener.HttpListenerException or System.Net.HttpListenerException or OperationCanceledException)
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
        return HttpListenerIsManaged ? new ManagedHttpListener() : new NativeHttpListener();
    }
}
