using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

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

    void RegisterCallback(string identifier, Func<ValueTask<IInfoQueryHandler>> callback);
    ValueTask<IInfoQueryHandler> FinishCallback(string? identifier);
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

    readonly ConcurrentDictionary<string, Func<ValueTask<IInfoQueryHandler>>> callbacks = new();

    public void RegisterCallback(string identifier, Func<ValueTask<IInfoQueryHandler>> callback)
    {
        if(!callbacks.TryAdd(identifier, callback))
        {
            throw new ArgumentException("The identifier was already used previously.", nameof(identifier));
        }
    }

    public ValueTask<IInfoQueryHandler> FinishCallback(string? identifier)
    {
        if(identifier == null || !callbacks.TryRemove(identifier, out var callback))
        {
            // Unidentified response cannot be dispatched
            return new(NullHandler.Instance);
        }
        return callback();
    }
}
