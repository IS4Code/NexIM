using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NexIM.Server.Net;

namespace NexIM.Server;

public class Configuration
{
    public static readonly bool PreserveUnavailableStatus = false;

    public static readonly TimeSpan XmppMinDelayTime = TimeSpan.FromSeconds(1);

    static Configuration()
    {
        // Used by SpaceWizards.HttpListener
#pragma warning disable SYSLIB0014
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
#pragma warning restore SYSLIB0014
    }

    public static bool OnUnexpectedException(Exception e)
    {
        if(e is SocketException or WebSocketException or SpaceWizards.HttpListener.HttpListenerException or System.Net.HttpListenerException or OperationCanceledException)
        {
            // Connection closed
            return true;
        }

        lock(typeof(Console))
        {
            Console.WriteLine(e);
        }
        Debugger.Break();
        return true;
    }

    public static IHttpListener CreateHttpListener()
    {
        return new ManagedHttpListener();
    }

    static readonly ConcurrentDictionary<string, X509Certificate2> selfSignedTemporaryCertificates = new(StringComparer.OrdinalIgnoreCase);
    static readonly Oid serverAuth = new("1.3.6.1.5.5.7.3.1");

    public static X509Certificate2 GetCertificate(string subjectName, TimeSpan? expires, IEnumerable<EndPoint>? endpoints)
    {
        // TODO File load/save
        return selfSignedTemporaryCertificates.GetOrAdd(subjectName ?? throw new ArgumentNullException(nameof(subjectName)), host => {
            using var rsa = RSA.Create();
            var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            SubjectAlternativeNameBuilder? san = null;
            foreach(var endpoint in endpoints ?? Array.Empty<EndPoint>())
            {
                switch(endpoint)
                {
                    case IPEndPoint ip:
                        (san ??= new()).AddIpAddress(ip.Address);
                        break;

                    case DnsEndPoint dns:
                        (san ??= new()).AddDnsName(dns.Host);
                        break;
                }
            }
            if(san != null)
            {
                req.CertificateExtensions.Add(san.Build());
            }
            
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { serverAuth },
                false
            ));

            var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.Add(expires ?? TimeSpan.FromDays(7)));

            // Load as persisted
            cert = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12, ""), "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            return cert;
        });
    }
}
