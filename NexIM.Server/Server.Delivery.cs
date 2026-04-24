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

    private ValueTuple Delivery {
        [MemberNotNull(nameof(accountTarget))]
        init {
            accountTarget = Route;
        }
    }

    public ValueTask<StatusReports> Post(Event evnt)
    {
        return evnt.To.Route(accountRouter, accountTarget, evnt);
    }

    private StatusReport Report(StatusCode code)
    {
        return new(default, code);
    }

    private bool IsServer(AccountName name)
    {
        return name.IsLocal && !name.IsUser;
    }

    private ValueTask<StatusReports> Route(AccountName accountName, Identifiers targetTo, Event evnt)
    {
        if(IsServer(accountName))
        {
            return RouteToSelf(evnt);
        }
        
        if(!accountName.IsUser)
        {
            // TODO Recognize other entities
            return new(Report(StatusCode.Unavailable));
        }

        if(GetAccount(accountName) is not { } account)
        {
            return new(Report(StatusCode.Unavailable));
        }

        // Deliver to the relevant account
        return account.Post(evnt.WithTo(targetTo));
    }

    private ValueTask<StatusReports> RouteToSelf(Event evnt)
    {
        switch(evnt)
        {
            case RequestEvent { Data: TimeData }:
                var processing = EventProcessing.Create();
                return Post(new ResponseEvent {
                    Origin = evnt.Origin.RespondFrom(AccountName.Local.ToIdentifier()),
                    Processing = processing,
                    Data = new TimeData {
                        DateTime = processing.Created.ToLocalTime()
                    }
                });

            default:
                return new(Report(StatusCode.Unavailable));
        }
    }
}
