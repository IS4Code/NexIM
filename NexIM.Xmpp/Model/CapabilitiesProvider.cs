using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Events;

namespace NexIM.Xmpp.Model;

internal sealed class CapabilitiesProvider : RemoteProvider<XmppCapabilities>, RemoteProvider<XmppCapabilities>.IResultRemoteProvider<CapabilitiesIdentifier>, IRemoteProvider<Capabilities>
{
    readonly CapabilitiesIdentifier identifier;
    readonly Task<XmppCapabilities> task;

    public CapabilitiesProvider(CapabilitiesIdentifier identifier, Task<XmppCapabilities> task)
    {
        // TODO Lazy loading
        this.identifier = identifier;
        this.task = task;
    }

    public override bool Equals(IRemoteProvider<XmppCapabilities> other)
    {
        return other is CapabilitiesProvider provider && identifier == provider.identifier;
    }

    public override bool Equals(XmppCapabilities? other)
    {
        return identifier == other?.Identifier;
    }

    public override int GetHashCode()
    {
        return identifier.GetHashCode();
    }

    protected override ValueTask<XmppCapabilities?> Load(CancellationToken cancellationToken)
    {
        return new(task!);
    }

    static readonly MemberInfo identifierMember =
        ((MemberExpression)((Expression<Func<XmppCapabilities, CapabilitiesIdentifier>>)(c => c.Identifier)).Body).Member;

    ValueTask<CapabilitiesIdentifier>? IResultRemoteProvider<CapabilitiesIdentifier>.TryGetImmediate(Expression<Func<XmppCapabilities, CapabilitiesIdentifier>> retrieveExpression, CancellationToken cancellationToken)
    {
        var argument = retrieveExpression.Parameters[0];
        switch(retrieveExpression.Body)
        {
            case MemberExpression { Expression: var param, Member: var member } when argument.Equals(param) && identifierMember.Equals(member):
                return new(identifier);
        }
        return null;
    }

    ValueTask<TResult> IRemoteProvider<Capabilities>.Get<TResult>(Expression<Func<Capabilities, TResult>> retrieveExpression, Func<TResult> defaultFactory, CancellationToken cancellationToken)
    {
        var param = Expression.Parameter(typeof(XmppCapabilities));
        var body = Expression.Invoke(retrieveExpression, param);
        return Get(Expression.Lambda<Func<XmppCapabilities, TResult>>(body, param), defaultFactory, cancellationToken);
    }

    bool IEquatable<IRemoteProvider<Capabilities>>.Equals(IRemoteProvider<Capabilities>? other)
    {
        return other is IRemoteProvider<XmppCapabilities> xmpp ? Equals(xmpp) : false;
    }

    bool IEquatable<Capabilities?>.Equals(Capabilities? other)
    {
        return other is XmppCapabilities xmpp ? Equals(xmpp) : false;
    }
}
