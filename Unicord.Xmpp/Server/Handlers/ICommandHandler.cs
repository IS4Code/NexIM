using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server.Handlers;

internal record struct CommandState(XmppServer Server, IXmppSession Session, string? Identifier);

internal interface ICommandHandler : IPayloadHandler
{
    CommandState State { get; init; }
}

internal interface IStanzaCommandHandler : ICommandHandler, IStanzaHandler
{
    StanzaType? Type { get; }
    XmppResource? From { get; }
    XmppResource? To { get; }
}
