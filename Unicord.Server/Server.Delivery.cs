using System;
using System.Threading.Tasks;
using Unicord.Server.Accounts;
using Unicord.Server.Events;

namespace Unicord.Server;

partial class Server
{
    static readonly Func<Identifier, AccountName> accountRouter = id => id.Account ?? default;
    readonly Func<AccountName, Identifiers, Event, ValueTask<StatusReports>> accountTarget;

    public ValueTask<StatusReports> Post(Event evnt)
    {
        return evnt.To.Route(accountRouter, accountTarget, evnt);
    }

    private StatusReport Report(StatusCode code)
    {
        return new(default, code);
    }

    private void InitDelivery(out Func<AccountName, Identifiers, Event, ValueTask<StatusReports>> accountTarget)
    {
        accountTarget = (accountName, accountTo, evnt) => {
            if(!accountName.IsValid)
            {
                // TODO Recognize other entities
                return new(Report(StatusCode.NotFound));
            }

            if(GetAccount(accountName) is not { } account)
            {
                return new(Report(StatusCode.NotFound));
            }

            // Deliver to the relevant account
            return account.Post(evnt.WithTo(accountTo));
        };
    }
}
