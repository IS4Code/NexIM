using System;
using System.Threading.Tasks;
using Unicord.Server.Accounts;
using Unicord.Server.Events;

namespace Unicord.Server;

partial class Server
{
    static readonly Func<Identifier, AccountName> accountRouter = id => id.Account ?? default;
    readonly Func<AccountName, IdentifierSet, Event, ValueTask<ErrorCode>> accountTarget;

    public ValueTask<ErrorCode> Post(Event evnt)
    {
        if(evnt.To.IsEmpty)
        {
            return new(ErrorCode.InvalidRequest);
        }

        return evnt.To.Route(accountRouter, accountTarget, evnt);
    }

    private void InitDelivery(out Func<AccountName, IdentifierSet, Event, ValueTask<ErrorCode>> accountTarget)
    {
        accountTarget = (accountName, accountTo, evnt) => {
            if(!accountName.IsValid)
            {
                // TODO Recognize other entities
                return new(ErrorCode.NotFound);
            }

            if(GetAccount(accountName) is not { } account)
            {
                return new(ErrorCode.NotFound);
            }

            // Deliver to the relevant account
            return account.Post(evnt.WithTo(accountTo));
        };
    }
}
