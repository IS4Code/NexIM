using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Server;
using NexIM.Server.Authentication;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Represents an outgoing IRC session.
/// </summary>
public interface IIrcSession : IClientNetworkSession<IrcClientSession>, IIrcSendingHandler, IAsyncDisposable
{

}

/// <summary>
/// Provides a basic implementation of <see cref="IIrcSession"/>.
/// </summary>
public abstract class IrcSession : IrcSendingHandler, IIrcSession
{
    public abstract bool Connected { get; }
    public abstract bool IsSecure { get; }
    public abstract EndPoint LocalEndPoint { get; }
    public abstract EndPoint RemoteEndPoint { get; }
    public abstract X509Certificate? RemoteCertificate { get; }
    public abstract CancellationToken CancellationToken { get; }

    public AccountName AccountName => ClientSession?.Account.Name /*?? RemoteResource?.Address.ToAccountName()*/ ?? throw new InvalidOperationException("This session has not been authenticated.");
    public IrcClientSession? ClientSession { get; set; }
    public SaslSession? SaslSession { get; set; }
    public bool IsAuthenticated => ClientSession != null;

    public abstract ValueTask DisposeAsync();
}
