using System.Threading.Tasks;
using NexIM.Server;
using NexIM.Server.Accounts;
using NexIM.Server.Events;

namespace NexIM.Irc.Server;

public class IrcClientSession : ClientSession
{
    public IrcClientSession(Account account, string? resource) : base(account, resource)
    {
    }

    protected override ValueTask<StatusCode> Write(Event evnt)
    {
        // TODO Events handling
        return new(StatusCode.Unavailable);
    }
}
