using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NexIM.App.Configuration;

namespace NexIM.Xmpp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var config = await ConfigurationReader.Read("config.xml");

        config.XmppReceiver.Server = new NexIM.Server.Server(config.SQLiteConnectionString ?? "Data Source=accounts.db");

        var tasks = new List<Task>();
        var cancellationToken = CancellationToken.None;
        
        if(config.XmppTcp is { } tcpListener)
        {
            tasks.Add(tcpListener.RunAsync(cancellationToken));
        }
        if(config.XmppWebSocket is { } wsListener)
        {
            tasks.Add(wsListener.RunAsync(cancellationToken));
        }
        if(config.XmppHtml is { } webListener)
        {
            tasks.Add(webListener.RunAsync(cancellationToken));
        }
        if(config.Metadata is { } metadataServer)
        {
            tasks.Add(metadataServer.RunAsync(cancellationToken));
        }

        await Task.WhenAll(tasks);
    }
}
