using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

internal class Message : MessageHandler, IStanzaCommandHandler
{
    readonly ConversationType? type;

    LocalizedString subject, body;
    string? nick;
    ChatState? state;

    public required CommandState State { get; init; }
    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    public Message(in Stanza stanza)
    {
        (Type, From, To) = this.OpenStanza(stanza);

        type = Type switch {
            null => null,
            StanzaType.Normal => ConversationType.Normal,
            StanzaType.Chat => ConversationType.Chat,
            StanzaType.GroupChat => ConversationType.GroupChat,
            StanzaType.Headline => ConversationType.Headline,
            StanzaType.Error => ConversationType.Error,
            _ => throw XmppStanzaException.BadRequest("Invalid message type.")
        };
    }

    protected async override ValueTask<bool> OnBody(LanguageTaggedString? text)
    {
        body = body.Add(text, State.Session.RemoteLanguage);
        return true;
    }

    protected async override ValueTask<bool> OnSubject(LanguageTaggedString? text)
    {
        subject = subject.Add(text, State.Session.RemoteLanguage);
        return true;
    }

    protected async override ValueTask<bool> OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
        return true;
    }

    protected async override ValueTask<bool> OnActive()
    {
        this.SetOnce(ref state, ChatState.Active);
        return true;
    }

    protected async override ValueTask<bool> OnInactive()
    {
        this.SetOnce(ref state, ChatState.Inactive);
        return true;
    }

    protected async override ValueTask<bool> OnComposing()
    {
        this.SetOnce(ref state, ChatState.Composing);
        return true;
    }

    protected async override ValueTask<bool> OnPaused()
    {
        this.SetOnce(ref state, ChatState.Paused);
        return true;
    }

    protected async override ValueTask<bool> OnGone()
    {
        this.SetOnce(ref state, ChatState.Gone);
        return true;
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        if(To is not { } to)
        {
            throw XmppStanzaException.BadRequest("Receiver of a message is empty.");
        }
        var targetAccount = ClientSession.GetAccount(to, out var targetIdentifier);
        if(State.Server.Sessions.GetSessions(targetAccount, targetIdentifier, false).FirstOrDefault() is not { } target)
        {
            throw XmppStanzaException.ItemNotFound("Receiver of a message is not connected.");
        }

        var sender = new Sender(
            Account: this.GetAccountName(),
            Identifier: this.GetRemoteResource().ResourceIdentifier,
            Presentation: new Unicord.Server.Model.SenderPresentation(Nickname: nick)
        );

        var message =
            (subject.Empty && body.Empty)
            ? null
            : new Unicord.Server.Model.Message(subject, body);

        await target.Conversation(sender, type, message, state);
    }
}
