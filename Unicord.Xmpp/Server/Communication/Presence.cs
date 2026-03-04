using System;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Presence : StanzaHandler, IPresenceHandler
{
    StatusType? show;
    LocalizedString statusText;
    string? nick;
    sbyte? priority;

    public Presence(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask IPresenceHandler.Show(Token<StatusType>? text)
    {
        SetOnce(ref show, text?.ToEnum());
    }

    async ValueTask IPresenceHandler.Status(LanguageTaggedString? text)
    {
        statusText.Add(text, Session.RemoteLanguage);
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

        if(Type is null or StanzaType.Unavailable)
        {
            // TODO Handle To
            var status = new Status(
                show switch {
                    StatusType.Chat => Availability.Chatting,
                    StatusType.Away => Availability.Away,
                    StatusType.ExtendedAway => Availability.Gone,
                    StatusType.DoNotDisturb => Availability.Busy,
                    _ => Type == StanzaType.Unavailable ? Availability.Unavailable : Availability.Available
                },
                statusText
            );
            if(Session.ClientSession?.UpdatePresence(sender, status) == true)
            {
                await Server.SendStatusProbe(Account, sender);
            }
            await Server.StatusUpdate(Account, RemoteResource.ResourceIdentifier, sender, status);
            return;
        }

        if(To is not { } to)
        {
            throw XmppStanzaException.BadRequest();
        }

        var target = ClientSession.GetAccount(to, out _);

        switch(Type)
        {
            case StanzaType.Subscribe:
                await Server.SendSubscribeRequest(Account, sender, target);
                break;
            case StanzaType.Subscribed:
                await Server.SendSubscribeResponse(Account, sender, target);
                break;
            case StanzaType.Unsubscribe:
                await Server.SendUnsubscribeNotification(Account, sender, target);
                break;
            case StanzaType.Unsubscribed:
                await Server.SendSubscribeCancellation(Account, sender, target);
                break;
            default:
                throw XmppStanzaException.FeatureNotImplemented();
        }
    }
}
