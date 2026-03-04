using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Primitives;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Message : StanzaHandler, IMessageHandler
{
    ConversationType? type;

    LocalizedString subject, body;

    string? nick;
    ChatState? state;

    public Message(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {
        type = stanza.Type?.Value switch
        {
            null => null,
            "normal" => ConversationType.Normal,
            "chat" => ConversationType.Chat,
            "groupchat" => ConversationType.GroupChat,
            "headline" => ConversationType.Headline,
            "error" => ConversationType.Error,
            _ => throw XmppStanzaException.BadRequest("Invalid message type.")
        };
    }

    async ValueTask IMessageHandler.Body(LanguageTaggedString? text)
    {
        body = body.Add(text, Session.RemoteLanguage);
    }

    async ValueTask IMessageHandler.Subject(LanguageTaggedString? text)
    {
        subject = subject.Add(text, Session.RemoteLanguage);
    }

    async ValueTask ISenderPresentation.Nickname(string? text)
    {
        SetOnce(ref nick, text);
    }

    async ValueTask IMessageHandler.Active()
    {
        SetOnce(ref state, ChatState.Active);
    }

    async ValueTask IMessageHandler.Inactive()
    {
        SetOnce(ref state, ChatState.Inactive);
    }

    async ValueTask IMessageHandler.Composing()
    {
        SetOnce(ref state, ChatState.Composing);
    }

    async ValueTask IMessageHandler.Paused()
    {
        SetOnce(ref state, ChatState.Paused);
    }

    async ValueTask IMessageHandler.Gone()
    {
        SetOnce(ref state, ChatState.Gone);
    }

    public async override ValueTask DisposeAsync()
    {
        if(To is not { } to)
        {
            throw XmppStanzaException.BadRequest("Receiver of a message is empty.");
        }
        var targetAccount = ClientSession.GetAccount(to, out var targetIdentifier);
        if(Server.Sessions.GetSessions(targetAccount, targetIdentifier, false).FirstOrDefault() is not { } target)
        {
            throw XmppStanzaException.ItemNotFound("Receiver of a message is not connected.");
        }

        var sender = new Sender(
            Account: AccountName,
            Identifier: RemoteResource.ResourceIdentifier,
            Presentation: new SenderPresentation(Nickname: nick)
        );

        var message =
            (subject.Empty && body.Empty)
            ? null
            : new Unicord.Server.Model.Message(subject, body);

        await target.Conversation(sender, type, message, state);
    }
}
