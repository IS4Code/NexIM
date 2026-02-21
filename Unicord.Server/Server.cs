using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Server.Primitives;

namespace Unicord.Server;

public class Server
{
    public SessionsManager Sessions { get; }
    public AccountsManager Accounts { get; }

    public Server(SessionsManager sessions, AccountsManager accounts)
    {
        Sessions = sessions;
        Accounts = accounts;
    }

    public async ValueTask<bool> Authenticate(AccountName accountName, TemporaryString? password, IClientSession session)
    {
        if(!await Accounts.Authenticate(accountName, password))
        {
            return false;
        }

        Sessions.AddSession(accountName, session);
        return true;
    }

    public async ValueTask<bool> RemoveContact(Account account, AccountName target)
    {
        if(account.RemoveContact(target) is not { } contact)
        {
            return false;
        }

        foreach(var session in Sessions.GetSessions(account.Name, null))
        {
            await session.ContactRemoved(contact);
        }

        return true;
    }

    public async ValueTask<bool> SetContact(Account account, Contact info)
    {
        if(account.SetContact(info) is not { } contact)
        {
            return false;
        }

        foreach(var session in Sessions.GetSessions(account.Name, null))
        {
            await session.ContactAdded(contact);
        }

        return true;
    }
}
