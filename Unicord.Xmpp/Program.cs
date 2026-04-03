using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var server = new XmppTcpListener(new XmppServer());

        await server.RunAsync();
    }

    internal static async ValueTask<TResult> NotImplemented<TResult>()
    {
        Debugger.Break();
        throw new NotImplementedException(null, XmppStanzaException.FeatureNotImplemented());
    }

    internal static bool OnUnexpectedException(Exception e)
    {
        lock(typeof(Console))
        {
            Console.WriteLine(e);
        }
        Debugger.Break();
        return true;
    }
}
