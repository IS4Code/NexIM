using System;
using System.Diagnostics;
using NexIM.Server.Net;

namespace NexIM.Server;

public class Configuration
{
    public static readonly bool PreserveUnavailableStatus = false;

    public static bool OnUnexpectedException(Exception e)
    {
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
