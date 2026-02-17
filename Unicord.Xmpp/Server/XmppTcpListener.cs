using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class XmppTcpListener : XmppListener<TcpClient>
{
    readonly TcpListener listener;

    public XmppTcpListener(IXmppReceiver<XmppStreamSession> receiver) : base(receiver)
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
            await HandleStream(client, cancellationToken);
        }
        catch(Exception e) when(!Debugger.IsAttached)
        {
            Console.WriteLine(e);
        }
        finally
        {
            client.Dispose();
        }
    }

    protected override ValueTask<XmppStreamSession> StartSession(TcpClient client, CancellationToken cancellationToken)
    {
        return new(new XmppServerSession(client, ReaderSettings, WriterSettings, cancellationToken));
    }
}
