global using ICommandHandler = Unicord.Xmpp.Protocol.Handlers.IPayloadHandler<Unicord.Xmpp.Server.Handlers.CommandContext>;

using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server.Handlers;

internal readonly record struct CommandContext(XmppServer Server, IXmppSession Session, string? Identifier) : IPayloadHandlerContext
{
    string IPayloadHandlerContext.DefaultNamespace => Session.DefaultNamespace;
}

internal interface IStanzaCommandHandler : ICommandHandler, IStanzaHandler
{
    StanzaType? Type { get; }
    XmppResource? From { get; }
    XmppResource? To { get; }
}
