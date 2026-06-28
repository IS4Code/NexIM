using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NexIM.Primitives;

namespace NexIM.Server.Security;

public abstract class CertificateSource
{
    public required TimeSpan RefreshDelay { get; init; }

    public abstract X509Certificate2 Load();

    public sealed class Static(X509Certificate2 certificate) : CertificateSource
    {
        public override X509Certificate2 Load()
        {
            return certificate;
        }
    }

    public sealed class SelfSigned : CertificateSource
    {
        static readonly Oid serverAuth = new("1.3.6.1.5.5.7.3.1");

        static readonly X509EnhancedKeyUsageExtension keyUsage = new(
            new OidCollection { serverAuth },
            false
        );

        readonly string subjectName;
        readonly TimeSpan issued, expires;
        readonly X509Extension? san;

        public SelfSigned(string subjectName, TimeSpan issued, TimeSpan expires, IEnumerable<EndPoint>? endpoints)
        {
            this.subjectName = subjectName;
            this.issued = issued;
            this.expires = expires;

            SubjectAlternativeNameBuilder? sanBuilder = null;
            foreach(var endpoint in endpoints ?? Array.Empty<EndPoint>())
            {
                switch(endpoint)
                {
                    case IPEndPoint ip:
                        (sanBuilder ??= new()).AddIpAddress(ip.Address);
                        break;

                    case DnsEndPoint dns:
                        (sanBuilder ??= new()).AddDnsName(dns.Host);
                        break;
                }
            }
            if(sanBuilder != null)
            {
                san = sanBuilder.Build();
            }
        }

        public override X509Certificate2 Load()
        {
            using var rsa = RSA.Create();
            var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if(san != null)
            {
                req.CertificateExtensions.Add(san);
            }

            req.CertificateExtensions.Add(keyUsage);

            var cert = req.CreateSelfSigned(DateTimeOffset.Now.Add(issued), DateTimeOffset.Now.Add(expires));

            // Load as persisted
            cert = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12, ""), "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            return cert;
        }
    }

    public sealed class FromStore(StoreName storeName, StoreLocation storeLocation, string subjectName) : CertificateSource
    {
        public override X509Certificate2 Load()
        {
            // Open the store
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
            return certificates.FirstOrDefault() ?? throw new CryptographicException($"The certificate for '{subjectName}' could not be found in {storeLocation}/{storeName}.");
        }
    }

    public sealed class FromFile(string certificatePath) : CertificateSource
    {
        public override X509Certificate2 Load()
        {
            return X509CertificateLoader.LoadCertificateFromFile(certificatePath);
        }
    }

    public sealed class FromPkcs12File(string certificatePath, TemporaryString password) : CertificateSource
    {
        public override X509Certificate2 Load()
        {
            return X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password.Value.AsSpan());
        }
    }

    public sealed class FromPemFile(string certificatePath, string? keyPath, TemporaryString? password) : CertificateSource
    {
        public override X509Certificate2 Load()
        {
            return password != null
                ? X509Certificate2.CreateFromEncryptedPemFile(certificatePath, password.Value.AsSpan(), keyPath)
                : X509Certificate2.CreateFromPemFile(certificatePath, keyPath);
        }
    }
}
