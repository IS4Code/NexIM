using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using NexIM.Server.Accounts;
using NexIM.Server.Events;

namespace NexIM.Server;

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

    private ValueTuple Delivery {
        [MemberNotNull(nameof(accountTarget))]
        init {
            accountTarget = (accountName, accountTo, evnt) => {
                if(!accountName.IsValid)
                {
                    // TODO Recognize other entities
                    return new(Report(StatusCode.Unavailable));
                }

                if(GetAccount(accountName) is not { } account)
                {
                    return new(Report(StatusCode.Unavailable));
                }

                // Deliver to the relevant account
                return account.Post(evnt.WithTo(accountTo));
            };
        }
    }
}
