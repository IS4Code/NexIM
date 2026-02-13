using System;
using System.Threading.Tasks;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public class ClientSession : IClientSession
{
    readonly IXmppSession xmpp;

    public sbyte Priority { get; set; }

    string IClientSession.Identifier => xmpp.RemoteResource?.ResourceIdentifier ?? throw new InvalidOperationException();

    public ClientSession(IXmppSession xmpp)
    {
        this.xmpp = xmpp;
    }

    async ValueTask IClientSession.Send(Sender sender, Message message)
    {
        var from = new XmppResource(sender);

        await using var msg = await xmpp.Message(new Stanza(From: from, To: xmpp.RemoteResource));
        if(message.Subject is { } subject)
        {
            await msg.Subject(subject);
        }
        if(message.Body is { } body)
        {
            await msg.Body(body);
        }
    }

    async ValueTask IClientSession.Notify(Sender sender, ChatState chatState)
    {
        var from = new XmppResource(sender);

        switch(chatState)
        {
            case ChatState.Active:
                await using(var msg = await Write())
                {
                    await msg.Active();
                    return;
                }
            case ChatState.Inactive:
                await using(var msg = await Write())
                {
                    await msg.Inactive();
                    return;
                }
            case ChatState.Composing:
                await using(var msg = await Write())
                {
                    await msg.Composing();
                    return;
                }
            case ChatState.Paused:
                await using(var msg = await Write())
                {
                    await msg.Paused();
                    return;
                }
            case ChatState.Gone:
                await using(var msg = await Write())
                {
                    await msg.Gone();
                    return;
                }
            default:
                // Unsupported notification type
                return;
        }

        ValueTask<IMessageHandler> Write()
        {
            return xmpp.Message(new Stanza(From: from, To: xmpp.RemoteResource));
        }
    }
}
