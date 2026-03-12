using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal class Presence : PresenceHandler<CommandContext>, IStanzaCommandHandler
{
    StatusType? show;
    LocalizedString statusText;
    string? nick;
    sbyte? priority;

    public required override CommandContext Context { get => base.Context; init => base.Context = value; }
    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    public Presence(in Stanza stanza)
    {
        (Type, From, To) = this.OpenStanza(stanza);
    }

    protected async override ValueTask OnShow(Token<StatusType>? text)
    {
        this.SetOnce(ref show, text?.ToEnum());
    }

    protected async override ValueTask OnStatus(LanguageTaggedString? text)
    {
        statusText.Add(text, Context.Session.RemoteLanguage);
    }

    protected async override ValueTask OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
    }

    protected async override ValueTask OnPriority(sbyte? value)
    {
        this.SetOnce(ref priority, value);
    }

    protected async override ValueTask OnDelay(DateTimeOffset? stamp, XmppResource? from, LanguageTaggedString? reason)
    {
        // Ignore
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        return this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        var session = Context.Session;
        var server = Context.Server;

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
