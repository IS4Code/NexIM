global using ICommandHandler = Unicord.Xmpp.Protocol.Handlers.IPayloadHandler<Unicord.Xmpp.Server.Handlers.ICommandContext>;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server.Handlers;

internal interface ICommandContext : IPayloadHandlerContext
{
    XmppServer Server { get; }
    IXmppSession Session { get; }
    Token<StanzaIdentifier>? Identifier { get; }

    string IPayloadHandlerContext.DefaultNamespace => Session.DefaultNamespace;
}

internal interface IStanzaCommandHandler : ICommandHandler, IStanzaHandler
{
    StanzaType? Type { get; }
    XmppResource? From { get; }
    XmppResource? To { get; }
}
