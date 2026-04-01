global using ICommandHandler = Unicord.Xmpp.Protocol.Handlers.IPayloadHandler<Unicord.Xmpp.Server.Handlers.ICommandContext>;
using System.Collections.Generic;
using Unicord.Primitives.Xml;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server.Handlers;

internal interface ICommandContext : IPayloadHandlerContext
{
    XmppServer Server { get; }
    IXmppSession Session { get; }
    Token<StanzaIdentifier>? Identifier { get; }
    ICollection<Event> EventsToSend { get; }

    string IPayloadHandlerContext.DefaultNamespace => Session.DefaultNamespace;
}

internal interface IStanzaCommandHandler : ICommandHandler, IStanzaHandler
{
    StanzaType? Type { get; }
    XmppResource? From { get; }
    XmppResource? To { get; }
}
