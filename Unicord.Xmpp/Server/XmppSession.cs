using System;
using System.Net;
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
    EndPoint? RemoteEndPoint { get; }

    AccountName AccountName { get; }
    ClientSession? ClientSession { get; set; }
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
    public abstract EndPoint? RemoteEndPoint { get; }
    public abstract CancellationToken CancellationToken { get; }

    public AccountName AccountName => ClientSession.GetAccount(RemoteResource?.Address ?? throw new InvalidOperationException("This session has not been authenticated."));
    public ClientSession? ClientSession { get; set; }
}
