using System;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Presence : StanzaHandler, IPresenceHandler
{
    string? show, status;
    sbyte? priority;

    public Presence(XmppServer server, IXmppSession session, in Stanza stanza) : base(server, session, stanza)
    {

    }

    async ValueTask IPresenceHandler.Show(string? text)
    {
        SetOnce(ref show, text);
    }

    async ValueTask IPresenceHandler.Status(string? text)
    {
        SetOnce(ref status, text);
    }

    async ValueTask IPresenceHandler.Priority(sbyte? value)
    {
        SetOnce(ref priority, value);
    }

    ValueTask IPresenceHandler.Delay(DateTimeOffset? stamp)
    {
        return default;
    }

    public async override ValueTask DisposeAsync()
    {
        if(priority is { } newPriority && Session.ClientSession is { } clientSession)
        {
            var currentPriority = clientSession.Priority;
            if(currentPriority != newPriority)
            {
                clientSession.Priority = newPriority;
                Server.Sessions.AddOrUpdateSession(Session.AccountName, Session.ClientSession);
            }
        }
    }
}
