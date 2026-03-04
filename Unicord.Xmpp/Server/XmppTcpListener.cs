using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Listens to TCP XMPP connections.
/// </summary>
public class XmppTcpListener : XmppServerListener<TcpClient, XmppStreamSession>
{
    protected override bool PrettyOutput => true;

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
            await Start(client, cancellationToken);
        }
        catch(Exception e) when(Program.SuppressUnexpectedExceptions())
        {
            Console.WriteLine(e);
        }
        finally
        {
            client.Dispose();
        }
    }

    protected override ValueTask<XmppStreamSession> CreateSession(TcpClient client, CancellationToken cancellationToken)
    {
        return new(new XmppTcpSession(client.GetStream(), ReaderSettings, WriterSettings, cancellationToken));
    }
}
