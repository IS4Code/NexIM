using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Primitives.Xml.Grammar;

namespace Unicord.Xmpp.Protocol;

[ComplexType]
public interface IInfoQueryHandler : IStanzaHandler
{
    [Name("query", IqRoster)]
    ValueTask<IRosterQueryHandler> RosterQuery([Name("ver")] string? version);

    [Name("query", IqAuth)]
    ValueTask<IAuthQueryHandler> AuthQuery();

    [Name("bind", XmppBind)]
    ValueTask<IBindHandler> Bind();

    [Name("session", XmppSession)]
    ValueTask Session();

    [Name("ping", "urn:xmpp:ping")]
    ValueTask Ping();
}

[ComplexType, Namespace(IqRoster)]
public interface IRosterQueryHandler : IPayloadHandler
{
    [Name("item")]
    ValueTask<IRosterItemHandler> Item(
        [Name("jid")] XmppResource? identifier,
        [Name("name")] string? name,
        [Name("subscription")] Token<RosterSubscriptionDirection>? subscription,
        [Name("ask")] Token<RosterPendingAction>? pending,
        [Name("approved")] bool? subscriptionApproved
    );
}

[SimpleType]
public enum RosterSubscriptionDirection
{
    [Name("none")] None,
    [Name("to")] To,
    [Name("from")] From,
    [Name("both")] Both,
    [Name("remove")] Remove
}

[SimpleType]
public enum RosterPendingAction
{
    [Name("subscribe")] Subscription
}

[ComplexType, Namespace(IqRoster)]
public interface IRosterItemHandler : IPayloadHandler
{
    [Name("group")]
    ValueTask Group(string? name);
}

[ComplexType, Namespace(IqAuth)]
public interface IAuthQueryHandler : IPayloadHandler
{
    [Name("username")]
    ValueTask Username(string? value);

    [Name("password")]
    ValueTask Password(TemporaryString? value);

    [Name("digest")]
    ValueTask Digest(string? value);

    [Name("resource")]
    ValueTask Resource(string? value);
}

[ComplexType, Namespace(XmppBind)]
public interface IBindHandler : IPayloadHandler
{
    [Name("resource")]
    ValueTask Resource(string? value);

    [Name("jid")]
    ValueTask Identifier(XmppResource? value);
}
