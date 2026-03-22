using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server;

partial class Server
{
    public ValueTask<ErrorCode> Post(Event evnt)
    {
        // TODO Recognize other entities

        if(evnt.To is not { Account: { } accountName })
        {
            return new(ErrorCode.InvalidRequest);
        }

        if(GetAccount(accountName) is not { } account)
        {
            return new(ErrorCode.NotFound);
        }

        return account.Post(evnt);
    }
}
