using System;
using System.Threading.Tasks;
using Unicord.Server.Accounts;
using Unicord.Server.Database;

namespace Unicord.Server;

partial class Server
{
    readonly AccountsContext database;

    private void InitDatabase(out AccountsContext database)
    {
        database = new(this);
        database.Database.EnsureCreated();

        // TODO Load only active ones
        foreach(var account in database.Accounts)
        {
            accounts[account.Name] = account;
        }
    }

    internal async Task SaveDatabase()
    {
        await database.SaveChangesAsync();
    }

    internal void AddUploadedFile(UploadedFile file)
    {
        database.UploadedFiles.Add(file);
    }

    internal UploadedFile? FindUploadedFile(Guid identifier)
    {
        return database.UploadedFiles.Find(identifier);
    }

    private Account CreateAccount(string user, string host, byte[] passwordHash)
    {
        var created = new Account(this, user, host, passwordHash);
        database.Accounts.Add(created);
        return created;
    }
}
