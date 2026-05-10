using System;
using System.Collections.Concurrent;
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

    public static X509Certificate2 GetCertificate(string host)
    {
        // TODO File load/save
        return selfSignedTemporaryCertificates.GetOrAdd(host ?? throw new ArgumentNullException(nameof(host)), static host => {
            using var rsa = RSA.Create();
            var req = new CertificateRequest("CN=" + host, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(7));

            // Load as persisted
            cert = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12, ""), "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            return cert;
        });
    }
}
