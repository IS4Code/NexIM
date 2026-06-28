using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NexIM.Primitives;
using NexIM.Server;
using NexIM.Server.Net;
using NexIM.Server.Security;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp.Server;

public class XmppWebServer : ICertificateTarget
{
    readonly IHttpListener listener;

    public XmppWebSocketListener? WebSocketListener { get; set; }
    public ICollection<string> Prefixes => listener.Prefixes;
    public X509Certificate2 Certificate {
        set => listener.Certificate = value;
    }

    public LanguageCode Language { get; set; } = new("en");
    public string Title { get; set; } = "XMPP Web Portal";
    public string Converse { get; set; } = "https://cdn.conversejs.org/dist/";

    IEnumerable<EndPoint> ICertificateTarget.EndPoints => CertificateHelper.PrefixesToEndPoints(Prefixes);

    public XmppWebServer()
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

            if(request.HttpMethod is not "GET")
            {
                // Ignore other methods
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                return;
            }

            var uri = request.Url;
            if(WebSocketListener?.GetEndpoints(uri).FirstOrDefault() is not { } endpoint)
            {
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
                return;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.AddHeader("Content-Type", "text/html");

            using var writer = new StreamWriter(response.OutputStream);
            await writer.WriteAsync($@"<!DOCTYPE html>
<html lang=""{HttpUtility.HtmlAttributeEncode(Language.Value)}"">
<head>
<title>{HttpUtility.HtmlEncode(Title)}</title>
<meta charset=""utf-8"">
<link rel=""stylesheet"" href=""{HttpUtility.HtmlAttributeEncode(Converse)}converse.min.css"">
<script src=""{HttpUtility.HtmlAttributeEncode(Converse)}converse.min.js"" charset=""utf-8""></script>
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
  i18n: {HttpUtility.JavaScriptStringEncode(Language.Value, true)},
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
