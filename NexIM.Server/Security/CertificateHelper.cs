using System;
using System.Collections.Generic;
using System.Net;

namespace NexIM.Server.Security;

public static class CertificateHelper
{
    public static IEnumerable<EndPoint> PrefixesToEndPoints(IEnumerable<string> prefixes)
    {
        foreach(var prefix in prefixes)
        {
            if(!Uri.TryCreate(prefix, UriKind.Absolute, out var uri))
            {
                // Wildcard domains not implemented currently
                continue;
            }
            switch(uri.HostNameType)
            {
                case UriHostNameType.Dns:
                    // DNS endpoint (punycode)
                    yield return new DnsEndPoint(uri.IdnHost, uri.Port);
                    break;
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    // IP endpoint
                    yield return IPEndPoint.Parse(uri.Authority);
                    break;
            }
        }
    }
}
