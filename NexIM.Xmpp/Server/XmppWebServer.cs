using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NexIM.Server;
using NexIM.Server.Net;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp.Server;

public class XmppWebServer
{
    readonly IHttpListener listener;
    readonly XmppWebSocketListener webSocketListener;

    public ICollection<string> Prefixes => listener.Prefixes;
    public X509Certificate2 Certificate {
        set => listener.Certificate = value;
    }

    public XmppWebServer(XmppWebSocketListener webSocketListener)
    {
        listener = Configuration.CreateHttpListener();
        this.webSocketListener = webSocketListener;
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

            if(request.HttpMethod is not "GET")
            {
                // Ignore other methods
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                return;
            }

            var uri = request.Url;
            if(webSocketListener.GetEndpoints(uri).FirstOrDefault() is not { } endpoint)
            {
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
                return;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.AddHeader("Content-Type", "text/html");

            using var writer = new StreamWriter(response.OutputStream);
            await writer.WriteAsync($@"<!DOCTYPE html>
<html>
<head>
<title>XMPP Web Portal</title>
<meta charset=""utf-8"">
<link rel=""stylesheet"" href=""https://cdn.conversejs.org/12.0.0/dist/converse.min.css"">
<script src=""https://cdn.conversejs.org/12.0.0/dist/converse.min.js"" charset=""utf-8""></script>
</head>
<body>
<script>
converse.initialize({{
  authentication: 'login',
  locked_domain: {HttpUtility.JavaScriptStringEncode(uri.Host, true)},
  discover_connection_methods: false,
  view_mode: 'fullscreen',
  show_background: true,
  priority: 5,
  show_retraction_warning: false,
  allow_non_roster_messaging: true,
  i18n: 'en',
  websocket_url: {HttpUtility.JavaScriptStringEncode(endpoint, true)}
}});
</script>
</body>
</html>");
        }
        catch(Exception e) when(Configuration.OnUnexpectedException(e))
        {

        }
    }
}
