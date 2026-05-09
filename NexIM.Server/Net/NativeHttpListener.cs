using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NexIM.Server.Net;

public sealed class NativeHttpListener : IHttpListener
{
    readonly HttpListener listener = new();

#nullable disable
    public ICollection<string> Prefixes => listener.Prefixes;
    public X509Certificate2 Certificate {
        set {
            // Ignore (must be done externally)
        }
    }

    public void Start() => listener.Start();
    public void Stop() => listener.Stop();
    public async Task<IHttpListenerContext> GetContextAsync() => new Context(await listener.GetContextAsync());

    sealed class Context(HttpListenerContext context) : IHttpListenerContext
    {
        public IHttpListenerRequest Request => new Request(context.Request);
        public IHttpListenerResponse Response => new Response(context.Response);

        public async Task<WebSocketContext> AcceptWebSocketAsync(string subProtocol)
        {
            return await context.AcceptWebSocketAsync(subProtocol);
        }
    }

    sealed class Request(HttpListenerRequest request) : IHttpListenerRequest
    {
        public string HttpMethod => request.HttpMethod;
        public Uri Url => request.Url;
        public string RawUrl => request.RawUrl;
        public string UserHostAddress => request.UserHostAddress;
        public string UserHostName => request.UserHostName;
        public EndPoint LocalEndPoint => request.LocalEndPoint;
        public EndPoint RemoteEndPoint => request.RemoteEndPoint;
        public bool IsSecureConnection => request.IsSecureConnection;
        public bool IsWebSocketRequest => request.IsWebSocketRequest;
        public NameValueCollection Headers => request.Headers;

        public Task<X509Certificate2> GetClientCertificateAsync()
        {
            return request.GetClientCertificateAsync();
        }
    }

    sealed class Response(HttpListenerResponse response) : IHttpListenerResponse
    {
        public string ContentType { get => response.ContentType; set => response.ContentType = value; }
        public bool KeepAlive { get => response.KeepAlive; set => response.KeepAlive = value; }
        public HttpStatusCode StatusCode { get => (HttpStatusCode)response.StatusCode; set => response.StatusCode = (int)value; }
        public string StatusDescription { get => response.StatusDescription; set => response.StatusDescription = value; }

        public Stream OutputStream => response.OutputStream;
        public CookieCollection Cookies => response.Cookies;
        public WebHeaderCollection Headers => response.Headers;

        public void AddHeader(string name, string value) => response.AddHeader(name, value);
        public void AppendHeader(string name, string value) => response.AppendHeader(name, value);
        public void Abort() => response.Abort();
        public void Close() => response.Close();
        void IDisposable.Dispose() => ((IDisposable)response).Dispose();
    }
#nullable restore
}
