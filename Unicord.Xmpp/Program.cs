using System.Threading.Tasks;
using Unicord.Metadata;
using Unicord.Xmpp.Server;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp;

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

        await Task.WhenAll(
            tcpListener.RunAsync(),
            wsListener.RunAsync(),
            metadataServer.RunAsync()
        );
    }
}
