using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server;

public class DeliveryManager(Server server)
{
    public async ValueTask<ErrorCode> Post(Event evnt)
    {
        // TODO Recognize other entities

        if(evnt.To is not (Account: { } account, Resource: var session))
        {
            return ErrorCode.InvalidRequest;
        }

        if(server.Sessions.GetSessions(account, session, false).FirstOrDefault() is not { } target)
        {
            return ErrorCode.NotFound;
        }

        return await target.Receive(evnt);
    }
}
