using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Protocol;

using static Constants;

static file class Constants
{
    public const string Client = "jabber:client";
    public const string IqRoster = "jabber:iq:roster";
    public const string IqAuth = "jabber:iq:auth";
}

[StructLayout(LayoutKind.Auto)]
public record struct Stanza(
    string? Type = null,
    XmppResource? From = null,
    XmppResource? To = null,
    string? Identifier = null
);

public interface IPayloadHandler : IAsyncDisposable
{
    ValueTask Other(XElement payload);
}

[ComplexType]
public interface IFeaturesHandler : IPayloadHandler
{
    [Name("auth", "http://jabber.org/features/iq-auth")]
    ValueTask IqAuth();
}

[ComplexType, Namespace(Client)]
public interface IMessageHandler : IPayloadHandler
{
    [Name("subject")]
    ValueTask Subject(string? text);

    [Name("body")]
    ValueTask Body(string? text);
}

[ComplexType, Namespace(Client)]
public interface IPresenceHandler : IPayloadHandler
{
    [Name("show")]
    ValueTask Show(string? text);

    [Name("status")]
    ValueTask Status(string? text);

    [Name("priority")]
    ValueTask Priority(sbyte? value);

    [Name("delay", "urn:xmpp:delay")]
    ValueTask Delay([Name("stamp")] DateTimeOffset stamp);
}

[ComplexType]
public interface IInfoQueryHandler : IPayloadHandler
{
    [Name("query", IqRoster)]
    ValueTask<IRosterQueryHandler> RosterQuery();

    [Name("query", IqAuth)]
    ValueTask<IAuthQueryHandler> AuthQuery();
}

[ComplexType, Namespace(IqRoster)]
public interface IRosterQueryHandler : IPayloadHandler
{
    [Name("item")]
    ValueTask Item(string? identifier);
}

[ComplexType, Namespace(IqAuth)]
public interface IAuthQueryHandler : IPayloadHandler
{
    [Name("username")]
    ValueTask Username(string? value);

    [Name("password")]
    ValueTask Password(string? value);

    [Name("digest")]
    ValueTask Digest(string? value);

    [Name("resource")]
    ValueTask Resource(string? value);
}

public enum MessageType
{
    Normal,
    Chat,
    GroupChat,
    Headline,
    Error
}

public enum ErrorType
{
    Auth,
    Cancel,
    Continue,
    Modify,
    Wait
}

public enum InfoQueryType
{
    Get,
    Set,
    Result,
    Error
}

public abstract class PayloadHandler : IPayloadHandler
{
    public abstract ValueTask DisposeAsync();

    public virtual ValueTask Other(XElement payload)
    {
        return default;
    }
}
