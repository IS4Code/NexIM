using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SpaceWizards.HttpListener;

namespace Unicord.Server.Net;

using EndPoint = System.Net.EndPoint;

public sealed class ManagedHttpListener : IHttpListener
{
    readonly HttpListener listener = new();

#nullable disable
    public ICollection<string> Prefixes => listener.Prefixes;

    public void Start() => listener.Start();
    public void Stop() => listener.Stop();
    public async Task<IHttpListenerContext> GetContextAsync() => new Context(await listener.GetContextAsync());

    sealed class Context(HttpListenerContext context) : IHttpListenerContext
    {
        public IHttpListenerRequest Request => new Request(context.Request);

        public async Task<WebSocketContext> AcceptWebSocketAsync(string subProtocol)
        {
            return await context.AcceptWebSocketAsync(subProtocol);
        }
    }

    sealed class Request(HttpListenerRequest request) : IHttpListenerRequest
    {
        public EndPoint LocalEndPoint => request.LocalEndPoint;
        public EndPoint RemoteEndPoint => request.RemoteEndPoint;
        public bool IsSecureConnection => request.IsSecureConnection;
        public bool IsWebSocketRequest => request.IsWebSocketRequest;

        public Task<X509Certificate2> GetClientCertificateAsync()
        {
            return request.GetClientCertificateAsync();
        }
    }
#nullable restore
}
