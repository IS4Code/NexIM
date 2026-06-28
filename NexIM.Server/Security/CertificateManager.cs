using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NexIM.Server.Security;

/// <summary>
/// Represents a component with a configurable certificate.
/// </summary>
public interface ICertificateTarget
{
    /// <summary>
    /// The list of endpoints the component requires the certificate for.
    /// </summary>
    IEnumerable<EndPoint> EndPoints { get; }

    /// <summary>
    /// The certificate the component authenticates with.
    /// </summary>
    X509Certificate2 Certificate { set; }
}

public sealed class CertificateManager
{
    readonly Source[] sources;

    public CertificateManager(IEnumerable<CertificateSource> sources)
    {
        this.sources = sources.Select(s => new Source(s)).ToArray();
    }

    public void Register(ICertificateTarget? target)
    {
        if(target == null || sources.Length == 0)
        {
            // No certificates to retrieve
            return;
        }

        // Look for best match among the sources
        Source match;

        var endpoints = target.EndPoints.ToList();
        if(endpoints.Count == 0 || sources.Length == 1)
        {
            // Use the first
            match = sources[0];
        }
        else
        {
            // Temporary sets of supported endpoints for each certificate
            var domains = new HashSet<string>();
            var ips = new HashSet<IPAddress>();

            bool Matches()
            {
                foreach(var endpoint in endpoints)
                {
                    // Check that endpoint is covered
                    switch(endpoint)
                    {
                        case IPEndPoint ip:
                            if(!ips.Contains(ip.Address))
                            {
                                return false;
                            }
                            break;

                        case DnsEndPoint dns:
                            if(!domains.Contains(dns.Host))
                            {
                                return false;
                            }
                            break;
                    }
                }
                return true;
            }

            foreach(var source in sources)
            {
                domains.Clear();
                ips.Clear();

                foreach(var extension in source.Certificate.Extensions)
                {
                    if(extension is not X509SubjectAlternativeNameExtension altName)
                    {
                        continue;
                    }
                    // Store all hosts this certificate covers
                    foreach(var dns in altName.EnumerateDnsNames())
                    {
                        domains.Add(dns);
                    }
                    foreach(var ip in altName.EnumerateIPAddresses())
                    {
                        ips.Add(ip);
                    }
                }

                if(Matches())
                {
                    // First certificate that covers all endpoints
                    match = source;
                    break;
                }
            }

            // Fallback to the first
            match = sources[0];
        }

        // Bind the target
        match.Register(target);
    }

    sealed class Source : IDisposable
    {
        readonly CertificateSource source;
        readonly Timer? refreshTimer;
        readonly List<ICertificateTarget> targets = new();

        public X509Certificate2 Certificate { get; private set; }

        public Source(CertificateSource source)
        {
            this.source = source;
            Certificate = source.Load();

            var refresh = source.RefreshDelay;
            refreshTimer = refresh <= TimeSpan.Zero ? null : new(static state => ((Source)state!).OnRefresh(), this, source.RefreshDelay, source.RefreshDelay);
        }

        public void Register(ICertificateTarget target)
        {
            target.Certificate = Certificate;
            targets.Add(target);
        }

        private void OnRefresh()
        {
            try
            {
                var refreshed = source.Load();
                Certificate = refreshed;

                // Update registered targets
                foreach(var target in targets)
                {
                    target.Certificate = refreshed;
                }
            }
            catch(Exception e)
            {
                lock(typeof(Console))
                {
                    Console.WriteLine("Certificate cannot be refreshed: " + e);
                }
            }
        }

        public void Dispose()
        {
            refreshTimer?.Dispose();
            Certificate?.Dispose();
        }
    }
}
