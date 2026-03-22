using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server;

partial class Server
{
    public async ValueTask<ErrorCode> Post(Event evnt)
    {
        // TODO Recognize other entities

        if(evnt.To is not (Account: { } accountName, Resource: var session))
        {
            return ErrorCode.InvalidRequest;
        }

        if(GetAccount(accountName) is not { } account)
        {
            return ErrorCode.NotFound;
        }

        if(account.GetSessions(session, false).FirstOrDefault() is not { } target)
        {
            return ErrorCode.NotFound;
        }

        return await target.Receive(evnt);
    }
}
