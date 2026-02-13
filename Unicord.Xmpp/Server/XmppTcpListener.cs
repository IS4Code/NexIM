using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class XmppTcpListener : XmppListener<TcpClient>
{
    readonly TcpListener listener;

    public XmppTcpListener(IXmppReceiver receiver) : base(receiver)
    {
        listener = new(IPAddress.Any, 5222);
    }

    public async override Task RunAsync(CancellationToken cancellationToken = default)
    {
        listener.Start();
        try
        {
            while(await listener.AcceptTcpClientAsync(cancellationToken) is { } client)
            {
                HandleClient(client, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    protected async void HandleClient(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = client.GetStream();

            await HandleStream(client, stream, cancellationToken);
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            client.Dispose();
        }
    }

    protected override ValueTask<XmppXmlSession> StartSession(TcpClient client, XmlWriter writer, CancellationToken cancellationToken)
    {
        return new(new XmppTcpXmlSession(client, writer, cancellationToken));
    }
}
