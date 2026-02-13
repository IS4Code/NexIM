using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Message : StanzaHandler, IMessageHandler
{
    string? subject, body;
    ChatState? state;

    public Message(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

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
            throw new XmppException("Receiver of a message is empty.", false);
        }
        var targetAccount = GetAccount(to, out var targetIdentifier);
        if(Server.Sessions.GetSessions(targetAccount, targetIdentifier).FirstOrDefault() is not { } target)
        {
            throw new XmppException("Receiver of a message is not connected.", false);
        }
        var sender = new Sender(targetAccount, targetIdentifier);
        if(subject != null || body != null)
        {
            await target.Send(sender, new Unicord.Server.Model.Message(subject, body));
        }
        if(state is { } newState)
        {
            await target.Notify(sender, newState);
        }
    }
}
