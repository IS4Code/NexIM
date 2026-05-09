using System.Threading.Tasks;
using NexIM.Metadata;
using NexIM.Server;
using NexIM.Xmpp.Server;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var server = new XmppServer();

        var certificate = Configuration.GetCertificate("localhost");

        var tcpListener = new XmppTcpListener(server);
        var wsListener = new XmppWebSocketListener(server);
        wsListener.Certificate = certificate;
        wsListener.Prefixes.Add("http://+:800/xmpp/");
        wsListener.Prefixes.Add("https://+:4430/xmpp/");

        var metadataServer = new WellKnownServices();
        metadataServer.Certificate = certificate;
        metadataServer.Prefixes.Add("http://+:800/.well-known/");
        metadataServer.Prefixes.Add("https://+:4430/.well-known/");
        metadataServer.MetadataProviders.Add(wsListener);

        var webServer = new XmppWebServer(wsListener);
        webServer.Certificate = certificate;
        webServer.Prefixes.Add("http://+:800/");
        webServer.Prefixes.Add("https://+:4430/");

        await Task.WhenAll(
            tcpListener.RunAsync(),
            wsListener.RunAsync(),
            metadataServer.RunAsync(),
            webServer.RunAsync()
        );
    }
}
