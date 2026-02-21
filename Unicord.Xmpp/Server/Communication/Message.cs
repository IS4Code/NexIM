using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Message : StanzaHandler, IMessageHandler
{
    ConversationType? type;

    string? subject, body;
    ChatState? state;

    public Message(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {
        type = stanza.Type switch
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

    async ValueTask IMessageHandler.Body(string? text)
    {
        SetOnce(ref body, text);
    }

    async ValueTask IMessageHandler.Subject(string? text)
    {
        SetOnce(ref subject, text);
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
        if(Server.Sessions.GetSessions(targetAccount, targetIdentifier).FirstOrDefault() is not { } target)
        {
            throw XmppStanzaException.ItemNotFound("Receiver of a message is not connected.");
        }
        var sender = new Sender(Session.AccountName, Session.RemoteResource?.ResourceIdentifier);

        var message =
            (subject != null || body != null)
            ? new Unicord.Server.Model.Message(subject, body)
            : null;

        await target.Conversation(sender, type, message, state);
    }
}
