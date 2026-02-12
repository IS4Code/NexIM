using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class GetRosterQuery : CommandHandler, IRosterQueryHandler
{
    public GetRosterQuery(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask IRosterQueryHandler.Item(string? identifier)
    {
        await Program.NotImplemented<object>();
    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await Session.InfoQuery(new(Type: "result", Identifier: Identifier));
    }
}
