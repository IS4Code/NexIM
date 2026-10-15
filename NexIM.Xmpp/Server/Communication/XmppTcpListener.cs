using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Server;
using NexIM.Server.Security;
using NexIM.Tools;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Listens to TCP XMPP connections.
/// </summary>
public class XmppTcpListener : XmppServerListener<TcpClient, XmppStreamSession>, ICertificateTarget
{
    protected override bool PrettyOutput => true;

    protected override ConformanceLevel ConformanceLevel => ConformanceLevel.Document;

    new XmppServerReceiver Receiver => (XmppServerReceiver)base.Receiver;

    public ICollection<IPEndPoint> EndPoints { get; } = new HashSet<IPEndPoint>();

    X509Certificate2? serverCertificate;
    X509Certificate2 ICertificateTarget.Certificate { set => serverCertificate = value; }
    IEnumerable<EndPoint> ICertificateTarget.EndPoints => EndPoints;

    public XmppTcpListener(XmppServerReceiver receiver) : base(receiver)
    {

    }

    public async override Task RunAsync(CancellationToken cancellationToken = default)
    {
        // One listener for every endpoint
        using TcpListeners listeners = new(EndPoints, cancellationToken);

        listeners.Start();

        while(await listeners.AcceptTcpClientAsync() is { } client)
        {
            HandleClient(client, cancellationToken);
        }
    }

    protected async void HandleClient(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            await Start(client, cancellationToken);
        }
        catch(Exception e) when(Configuration.OnUnexpectedException(e))
        {

        }
        finally
        {
            client.Dispose();
        }
    }

    protected override ValueTask<XmppStreamSession> CreateSession(TcpClient client, CancellationToken cancellationToken)
    {
        return new(new XmppTcpSession(Receiver, client.GetStream(), serverCertificate, ReaderSettings, WriterSettings, cancellationToken));
    }
}
