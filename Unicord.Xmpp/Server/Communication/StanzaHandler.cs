using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication
{
    internal abstract class StanzaHandler : CommandHandler, IStanzaHandler
    {
        public string? Type { get; }
        public XmppResource? From { get; }
        public XmppResource? To { get; }

        public StanzaHandler(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza.Identifier)
        {
            (Type, From, To, _) = stanza;
        }

        ValueTask<IStanzaErrorHandler> IStanzaHandler.Error(string? type)
        {
            return Program.NotImplemented<IStanzaErrorHandler>();
        }
    }
}
