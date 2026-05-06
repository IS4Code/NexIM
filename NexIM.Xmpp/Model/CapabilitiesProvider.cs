using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Events;

namespace NexIM.Xmpp.Model;

internal sealed class CapabilitiesProvider : IdentifierRemoteProvider<Capabilities, XmppCapabilities, CapabilitiesIdentifier>
{
    readonly Task<XmppCapabilities> task;

    protected override CapabilitiesIdentifier Identifier { get; }
    protected override MemberInfo IdentifierMember => identifierMember;

    public CapabilitiesProvider(CapabilitiesIdentifier identifier, Task<XmppCapabilities> task)
    {
        // TODO Lazy loading
        this.task = task;
        Identifier = identifier;
    }

    public override bool References(XmppCapabilities? other)
    {
        return Identifier == other?.Identifier;
    }

    protected override ValueTask<XmppCapabilities?> Load(CancellationToken cancellationToken)
    {
        return new(task!);
    }

    static readonly MemberInfo identifierMember =
        ((MemberExpression)((Expression<Func<XmppCapabilities, CapabilitiesIdentifier>>)(c => c.Identifier)).Body).Member;
}
