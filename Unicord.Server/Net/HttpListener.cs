using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Unicord.Server.Net;

public interface IHttpListener
{
    ICollection<string> Prefixes { get; }

    void Start();
    void Stop();
    Task<IHttpListenerContext> GetContextAsync();
}

public interface IHttpListenerContext
{
    IHttpListenerRequest Request { get; }
    Task<WebSocketContext> AcceptWebSocketAsync(string? subProtocol);
}

public interface IHttpListenerRequest
{
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
    bool IsSecureConnection { get; }
    bool IsWebSocketRequest { get; }

    Task<X509Certificate2?> GetClientCertificateAsync();
}
