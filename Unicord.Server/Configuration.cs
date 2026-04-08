using System;
using System.Diagnostics;
using Unicord.Server.Net;

namespace Unicord.Server;

public class Configuration
{
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
