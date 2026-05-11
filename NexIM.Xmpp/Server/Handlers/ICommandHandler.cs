global using ICommandHandler = NexIM.Primitives.Xml.Handlers.IPayloadHandler<NexIM.Xmpp.Server.Handlers.ICommandContext>;
using System;
using System.Collections.Generic;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp.Server.Handlers;

internal interface ICommandContext : IPayloadHandlerContext
{
    XmppServerReceiver ServerReceiver { get; }
    IXmppSession Session { get; }
    ref readonly Stanza LastStanza { get; }
    DateTimeOffset LastStanzaReceived { get; }
    ICollection<Event> EventsToSend { get; }

    string IPayloadHandlerContext.DefaultNamespace => Session.DefaultNamespace;
}
