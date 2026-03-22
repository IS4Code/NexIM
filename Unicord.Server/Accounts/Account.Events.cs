using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

partial class Account : IEventHandler
{
    public ValueTask<ErrorCode> Post(Event evnt)
    {
        // TODO Message duplicating logic (carbons)

        if(evnt.To is not { Account: { } accountName, Resource: var session } || accountName != Name)
        {
            // Not intended for this account
            return server.Post(evnt);
        }

        // Local delivery - find the proper session
        if(GetSessions(session, false).FirstOrDefault() is not { } targetSession)
        {
            return new(ErrorCode.NotFound);
        }
        return targetSession.Receive(evnt);
    }
}
