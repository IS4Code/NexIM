using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Primitives.Xml;
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
    XmppClientSession? ClientSession { get; set; }

    [MemberNotNullWhen(true, nameof(ClientSession))]
    bool IsAuthenticated { get; }

    void RegisterCallback(Token<StanzaIdentifier> identifier, Func<ValueTask<IInfoQueryHandler>> callback);
    ValueTask<IInfoQueryHandler>? FinishCallback(Token<StanzaIdentifier>? identifier);

    Token<T> GetToken<T>(string value) where T : Enum => GetToken<T>(value.AsMemory());
    Token<T> GetToken<T>(ReadOnlyMemory<char> value) where T : Enum;
    Token<T> GetToken<T>(ReadOnlySpan<char> value) where T : Enum;
    Token<StanzaIdentifier> NewStanzaIdentifier();
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

    public AccountName AccountName => ClientSession?.Account.Name ?? RemoteResource?.Address.ToAccountName() ?? throw new InvalidOperationException("This session has not been authenticated.");
    public XmppClientSession? ClientSession { get; set; }
    public bool IsAuthenticated => ClientSession != null;

    public Token<T> GetToken<T>(string value) where T : Enum => GetToken<T>(value.AsMemory());
    public abstract Token<T> GetToken<T>(ReadOnlyMemory<char> value) where T : Enum;
    public abstract Token<T> GetToken<T>(ReadOnlySpan<char> value) where T : Enum;

    public Token<StanzaIdentifier> NewStanzaIdentifier()
    {
        return GetToken<StanzaIdentifier>(Guid.NewGuid().ToString("N"));
    }

    readonly ConcurrentDictionary<string, Func<ValueTask<IInfoQueryHandler>>> callbacks = new(ReferenceEqualityComparer.Instance);

    public void RegisterCallback(Token<StanzaIdentifier> identifier, Func<ValueTask<IInfoQueryHandler>> callback)
    {
        if(!callbacks.TryAdd(identifier.Value, callback))
        {
            throw new ArgumentException("The identifier was already used previously.", nameof(identifier));
        }
    }

    public ValueTask<IInfoQueryHandler>? FinishCallback(Token<StanzaIdentifier>? identifier)
    {
        if(identifier is not { Value: var value } || !callbacks.TryRemove(value, out var callback))
        {
            // Unidentified response cannot be dispatched
            return null;
        }
        return callback();
    }
}
