using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using NexIM.Server.Authentication;

namespace NexIM.Server;

/// <summary>
/// Represents a maintained network session to a remote.
/// </summary>
public interface INetworkSession
{
    bool Connected { get; }
    bool IsSecure { get; }
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
    X509Certificate? RemoteCertificate { get; }
}

/// <summary>
/// Represents a network session to a client-side remote.
/// </summary>
public interface IClientNetworkSession : INetworkSession
{
    AccountName AccountName { get; }
    ClientSession? ClientSession { get; }
    SaslSession? SaslSession { get; set; }

    [MemberNotNullWhen(true, nameof(ClientSession))]
    bool IsAuthenticated => ClientSession != null;
}

/// <summary>
/// Represents a network session to a client-side remote.
/// </summary>
/// <typeparam name="TClientSession">The type of the authenticated client session.</typeparam>
public interface IClientNetworkSession<TClientSession> : IClientNetworkSession where TClientSession : ClientSession
{
    new TClientSession? ClientSession { get; set; }
    ClientSession? IClientNetworkSession.ClientSession => ClientSession;

    [MemberNotNullWhen(true, nameof(ClientSession))]
    new bool IsAuthenticated => ClientSession != null;
}
