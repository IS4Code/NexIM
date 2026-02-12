using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Message : StanzaHandler, IMessageHandler
{
    public Message(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask IMessageHandler.Body(string? text)
    {
        await Program.NotImplemented<object>();
    }

    async ValueTask IMessageHandler.Subject(string? text)
    {
        await Program.NotImplemented<object>();
    }

    public async override ValueTask DisposeAsync()
    {

    }
}
