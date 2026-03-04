using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Unicord.Server;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Represents an outgoing XMPP session, i.e. a channel that supports
/// sending XMPP commands to a remote entity.
/// </summary>
public interface IXmppSession : IXmppSendingHandler
{
    bool Connected { get; }
    bool IsSecure { get; }
    bool CanUpgradeTls { get; }
    bool CanCompress { get; }
    EndPoint? LocalEndPoint { get; }
    EndPoint? RemoteEndPoint { get; }
    X509Certificate? RemoteCertificate { get; }

    AccountName AccountName { get; }
    ClientSession? ClientSession { get; set; }

    [MemberNotNullWhen(true, nameof(ClientSession))]
    bool IsAuthenticated { get; }
}

/// <summary>
/// Provides a basic implementation of <see cref="IXmppSession"/>.
/// </summary>
public abstract class XmppSession : XmppSendingHandler, IXmppSession
{
    public abstract bool Connected { get; }
    public abstract bool IsSecure { get; }
    public abstract bool CanUpgradeTls { get; }
    public abstract bool CanCompress { get; }
    public abstract EndPoint? LocalEndPoint { get; }
    public abstract EndPoint? RemoteEndPoint { get; }
    public abstract X509Certificate? RemoteCertificate { get; }
    public abstract CancellationToken CancellationToken { get; }

    public AccountName AccountName => ClientSession?.AccountName ?? ClientSession.GetAccount(RemoteResource?.Address ?? throw new InvalidOperationException("This session has not been authenticated."));
    public ClientSession? ClientSession { get; set; }
    public bool IsAuthenticated => ClientSession != null;
}
