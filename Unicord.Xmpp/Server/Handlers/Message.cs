using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal class Message : MessageHandler<CommandContext>, IStanzaCommandHandler
{
    readonly ConversationType? type;

    LocalizedString subject, body;
    string? nick;
    ChatState? state;

    public required override CommandContext Context { get => base.Context; init => base.Context = value; }
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

    protected async override ValueTask OnBody(LanguageTaggedString? text)
    {
        body = body.Add(text, Context.Session.RemoteLanguage);
    }

    protected async override ValueTask OnSubject(LanguageTaggedString? text)
    {
        subject = subject.Add(text, Context.Session.RemoteLanguage);
    }

    protected async override ValueTask OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
    }

    protected async override ValueTask OnActive()
    {
        this.SetOnce(ref state, ChatState.Active);
    }

    protected async override ValueTask OnInactive()
    {
        this.SetOnce(ref state, ChatState.Inactive);
    }

    protected async override ValueTask OnComposing()
    {
        this.SetOnce(ref state, ChatState.Composing);
    }

    protected async override ValueTask OnPaused()
    {
        this.SetOnce(ref state, ChatState.Paused);
    }

    protected async override ValueTask OnGone()
    {
        this.SetOnce(ref state, ChatState.Gone);
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
        if(Context.Server.Sessions.GetSessions(targetAccount, targetIdentifier, false).FirstOrDefault() is not { } target)
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
