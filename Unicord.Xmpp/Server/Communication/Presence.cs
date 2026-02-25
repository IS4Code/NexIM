using System;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Presence : StanzaHandler, IPresenceHandler
{
    string? show, status, nick;
    sbyte? priority;

    public Presence(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask IPresenceHandler.Show(Token<StatusType>? text)
    {
        SetOnce(ref show, text);
    }

    async ValueTask IPresenceHandler.Status(string? text)
    {
        SetOnce(ref status, text);
    }

    async ValueTask ISenderPresentation.Nickname(string? text)
    {
        SetOnce(ref nick, text);
    }

    async ValueTask IPresenceHandler.Priority(sbyte? value)
    {
        SetOnce(ref priority, value);
    }

    ValueTask IPresenceHandler.Delay(DateTimeOffset? stamp)
    {
        return default;
    }

    public async override ValueTask DisposeAsync()
    {
        if(priority is { } newPriority && Session.ClientSession is { } clientSession)
        {
            var currentPriority = clientSession.Priority;
            if(currentPriority != newPriority)
            {
                clientSession.Priority = newPriority;
                Server.Sessions.AddOrUpdateSession(Session.AccountName, Session.ClientSession);
            }
        }

        var sender = new SenderPresentation(Nickname: nick);

        if(Type == null)
        {
            // TODO Handle To
            Session.ClientSession?.SubscribeToPresenceUpdates();
            await Server.StatusUpdate(Account, RemoteResource.ResourceIdentifier, sender, new Status(
                show switch
                {
                    "chat" => Availability.Chatting,
                    "away" => Availability.Away,
                    "xa" => Availability.Gone,
                    "dnd" => Availability.Busy,
                    _ => Availability.Available
                },
                status
            ));
            return;
        }

        if(To is not { } to)
        {
            throw XmppStanzaException.BadRequest();
        }

        var target = ClientSession.GetAccount(to, out _);

        switch(Type)
        {
            case "subscribe":
                await Server.SendSubscribeRequest(Account, sender, target);
                break;
            case "subscribed":
                await Server.SendSubscribeResponse(Account, sender, target);
                break;
            case "unsubscribe":
                await Server.SendUnsubscribeNotification(Account, sender, target);
                break;
            case "unsubscribed":
                await Server.SendSubscribeCancellation(Account, sender, target);
                break;
            default:
                throw XmppStanzaException.FeatureNotImplemented();
        }
    }
}
