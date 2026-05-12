using Microsoft.EntityFrameworkCore;
using NexIM.Server.Accounts;
using System.Linq;

namespace NexIM.Server.Database;

internal sealed class AccountContext(NexServer server) : DatabaseContext(server)
{
    public IQueryable<Account> FullAccounts => Accounts
        .Include(x => x.Identity)
        .Include(x => x.ContactsBuilder)
        .ThenInclude(x => x.Identity)
        .Include(x => x.PrivateStorageBuilder)
        .Include(x => x.UploadedFilesBuilder);
}
