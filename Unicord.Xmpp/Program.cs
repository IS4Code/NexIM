using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server;

namespace Unicord.Xmpp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var server = new XmppTcpListener(new XmppServer(new(), new()));

        await server.RunAsync();
    }

    internal static async ValueTask<TResult> NotImplemented<TResult>()
    {
        Debugger.Break();
        throw new NotImplementedException(null, XmppStanzaException.FeatureNotImplemented());
    }
}
