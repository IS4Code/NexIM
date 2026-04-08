using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
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
    IHttpListenerResponse Response { get; }
    Task<WebSocketContext> AcceptWebSocketAsync(string? subProtocol);
}

public interface IHttpListenerRequest
{
    string HttpMethod { get; }
    Uri Url { get; }
    string RawUrl { get; }
    string UserHostAddress { get; }
    string UserHostName { get; }
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
    bool IsSecureConnection { get; }
    bool IsWebSocketRequest { get; }
    NameValueCollection Headers { get; }

    Task<X509Certificate2?> GetClientCertificateAsync();
}

public interface IHttpListenerResponse : IDisposable
{
    string ContentType { get; set; }
    bool KeepAlive { get; set; }
    HttpStatusCode StatusCode { get; set; }
    string StatusDescription { get; set; }

    Stream OutputStream { get; }
    CookieCollection Cookies { get; }
    WebHeaderCollection Headers { get; }

    void AddHeader(string name, string value);
    void AppendHeader(string name, string value);
    void Abort();
    void Close();
}
