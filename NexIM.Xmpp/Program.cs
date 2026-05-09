using System.Threading.Tasks;
using NexIM.Metadata;
using NexIM.Xmpp.Server;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var server = new XmppServer();

        var tcpListener = new XmppTcpListener(server);
        var wsListener = new XmppWebSocketListener(server);
        wsListener.Prefixes.Add("http://+:800/xmpp/");

        var metadataServer = new WellKnownServices();
        metadataServer.Prefixes.Add("http://+:800/.well-known/");
        metadataServer.MetadataProviders.Add(wsListener);

        var webServer = new XmppWebServer(wsListener);
        webServer.Prefixes.Add("http://+:800/");

        await Task.WhenAll(
            tcpListener.RunAsync(),
            wsListener.RunAsync(),
            metadataServer.RunAsync(),
            webServer.RunAsync()
        );
    }
}
