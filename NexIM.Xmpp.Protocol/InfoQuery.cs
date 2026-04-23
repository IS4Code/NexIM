using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType]
public interface IInfoQueryHandler : IStanzaHandler
{
    [Name("query", IqRoster)]
    ValueTask<IRosterQueryHandler> RosterQuery([Name("ver")] string? version);

    [Name("query", IqAuth)]
    ValueTask<IAuthQueryHandler> AuthQuery();

    [Name("query", DiscoInfo)]
    ValueTask<IDiscoInfoQueryHandler> DiscoInfoQuery([Name("node")] Token<DiscoNode>? node);

    [Name("query", DiscoItems)]
    ValueTask<IDiscoItemsQueryHandler> DiscoItemsQuery([Name("node")] Token<DiscoNode>? node);

    [Name("bind", XmppBind)]
    ValueTask<IBindHandler> Bind();

    [Name("session", XmppSession)]
    ValueTask Session();

    [Name("ping", XmppPing)]
    ValueTask Ping();

    [Name("time", XmppTime)]
    ValueTask<ITimeHandler> Time();

    [Name("vCard", VCardTemp)]
    ValueTask<IVCardHandler> VCard();

    [Name("query", "jabber:iq:private")]
    ValueTask<IPrivateStorageHandler> PrivateQuery();
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

[ComplexType, Namespace(XmppTime)]
public interface ITimeHandler : IPayloadHandler
{
    [Name("tzo")]
    ValueTask TimeZoneOffset(TimeZoneOffset? offset);

    [Name("utc")]
    ValueTask UtcTime(DateTime? time);
}

[ComplexType]
public interface IPrivateStorageHandler : IPayloadHandler
{

}
