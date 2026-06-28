using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Server;
using NexIM.Server.Net;
using NexIM.Server.Security;

namespace NexIM.Metadata;

public partial class WellKnownServices : ICertificateTarget
{
    readonly IHttpListener listener;

    public ICollection<string> Prefixes => listener.Prefixes;
    public X509Certificate2 Certificate {
        set => listener.Certificate = value;
    }

    IEnumerable<EndPoint> ICertificateTarget.EndPoints => CertificateHelper.PrefixesToEndPoints(Prefixes);

    public WellKnownServices()
    {
        listener = Configuration.CreateHttpListener();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        listener.Start();
        try
        {
            cancellationToken.Register(listener.Stop);

            while(await listener.GetContextAsync() is { } context)
            {
                HandleContext(context, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    protected async void HandleContext(IHttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var request = context.Request;
            using var response = context.Response;

            try
            {
                switch(request.Url.LocalPath)
                {
                    case "/.well-known/host-meta":
                        await GetHostMeta(context, false, cancellationToken);
                        break;
                    case "/.well-known/host-meta.json":
                        await GetHostMeta(context, true, cancellationToken);
                        break;
                    default:
                        // No other well-known services supported
                        response.StatusCode = HttpStatusCode.NotFound;
                        break;
                }
            }
            catch(FormatException)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
            }
        }
        catch(Exception e) when(Configuration.OnUnexpectedException(e))
        {

        }
    }
}
