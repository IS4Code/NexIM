using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Server;

namespace Unicord.Xmpp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var sessions = new SessionsManager();
        var server = new XmppTcpListener(new XmppServer(sessions));

        await server.RunAsync();
    }

    internal static async ValueTask<TResult> NotImplemented<TResult>()
    {
        Debugger.Break();
        throw new NotImplementedException();
    }
}
