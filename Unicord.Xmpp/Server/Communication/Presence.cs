using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

internal class Presence : PresenceHandler, IStanzaCommandHandler
{
    StatusType? show;
    LocalizedString statusText;
    string? nick;
    sbyte? priority;

    public required CommandState State { get; init; }
    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    public Presence(in Stanza stanza)
    {
        (Type, From, To) = this.OpenStanza(stanza);
    }

    protected async override ValueTask<bool> OnShow(Token<StatusType>? text)
    {
        this.SetOnce(ref show, text?.ToEnum());
        return true;
    }

    protected async override ValueTask<bool> OnStatus(LanguageTaggedString? text)
    {
        statusText.Add(text, State.Session.RemoteLanguage);
        return true;
    }

    protected async override ValueTask<bool> OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
        return true;
    }

    protected async override ValueTask<bool> OnPriority(sbyte? value)
    {
        this.SetOnce(ref priority, value);
        return true;
    }

    protected async override ValueTask<bool> OnDelay(DateTimeOffset? stamp)
    {
        return true;
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        return this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        var session = State.Session;
        var server = State.Server;

        if(priority is { } newPriority && session.ClientSession is { } clientSession)
        {
            var currentPriority = clientSession.Priority;
            if(currentPriority != newPriority)
            {
                clientSession.Priority = newPriority;
                server.Sessions.AddOrUpdateSession(session.AccountName, session.ClientSession);
            }
        }

        var sender = new Unicord.Server.Model.SenderPresentation(Nickname: nick);

        var account = this.GetAccount();

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
            if(session.ClientSession?.UpdatePresence(sender, status) == true)
            {
                await server.SendStatusProbe(account, sender);
            }
            await server.StatusUpdate(account, this.GetRemoteResource().ResourceIdentifier, sender, status);
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
                await server.SendSubscribeRequest(account, sender, target);
                break;
            case StanzaType.Subscribed:
                await server.SendSubscribeResponse(account, sender, target);
                break;
            case StanzaType.Unsubscribe:
                await server.SendUnsubscribeNotification(account, sender, target);
                break;
            case StanzaType.Unsubscribed:
                await server.SendSubscribeCancellation(account, sender, target);
                break;
            default:
                throw XmppStanzaException.FeatureNotImplemented();
        }
    }
}
