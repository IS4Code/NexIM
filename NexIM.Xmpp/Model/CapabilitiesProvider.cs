using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Events;

namespace NexIM.Xmpp.Model;

internal sealed class CapabilitiesProvider : DerivedRemoteProvider<Capabilities, XmppCapabilities>, RemoteProvider<XmppCapabilities>.IResultRemoteProvider<CapabilitiesIdentifier>
{
    readonly CapabilitiesIdentifier identifier;
    readonly Task<XmppCapabilities> task;

    public CapabilitiesProvider(CapabilitiesIdentifier identifier, Task<XmppCapabilities> task)
    {
        // TODO Lazy loading
        this.identifier = identifier;
        this.task = task;
    }

    public override bool Equals(IRemoteProvider other)
    {
        return other is CapabilitiesProvider provider && identifier == provider.identifier;
    }

    public override bool References(XmppCapabilities? other)
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

    ValueTask<CapabilitiesIdentifier>? IResultRemoteProvider<CapabilitiesIdentifier>.TryGetImmediate(LambdaExpression retrieveExpression, CancellationToken cancellationToken)
    {
        var argument = retrieveExpression.Parameters[0];
        switch(retrieveExpression.Body)
        {
            case MemberExpression { Expression: var param, Member: var member } when argument.Equals(param) && identifierMember.Equals(member):
                return new(identifier);
        }
        return null;
    }
}
