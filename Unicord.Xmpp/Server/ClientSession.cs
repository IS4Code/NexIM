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

    private string? MessageType(ConversationType? type)
    {
        return type switch
        {
            ConversationType.Normal => "normal",
            ConversationType.Chat => "chat",
            ConversationType.GroupChat => "groupchat",
            ConversationType.Headline => "headline",
            ConversationType.Error => "error",
            _ => null
        };
    }

    async ValueTask IClientSession.Conversation(Sender sender, ConversationType? type, Message? message, ChatState? chatState)
    {
        var from = new XmppResource(sender);

        if(message == null)
        {
            // Activity with no message
            await Notify(from, type, chatState);
            return;
        }

        await using var msg = await xmpp.Message(new Stanza(From: from, To: xmpp.RemoteResource, Type: MessageType(type)));
        
        if(message.Subject is { } subject)
        {
            await msg.Subject(subject);
        }
        if(message.Body is { } body)
        {
            await msg.Body(body);
        }

        switch(chatState)
        {
            case ChatState.Active:
                await msg.Active();
                break;
            case ChatState.Inactive:
                await msg.Inactive();
                break;
            case ChatState.Composing:
                await msg.Composing();
                break;
            case ChatState.Paused:
                await msg.Paused();
                break;
            case ChatState.Gone:
                await msg.Gone();
                break;
            default:
                break;
        }
    }

    private async ValueTask Notify(XmppResource from, ConversationType? type, ChatState? chatState)
    {
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
                // Unsupported notification type does not need to cause a message
                return;
        }

        ValueTask<IMessageHandler> Write()
        {
            return xmpp.Message(new Stanza(From: from, To: xmpp.RemoteResource, Type: MessageType(type)));
        }
    }
}
