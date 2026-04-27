using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using NexIM.Server.Accounts;
using NexIM.Server.Database;

namespace NexIM.Server;

partial class Server
{
    readonly AccountsContext database;

    private ValueTuple Database {
        [MemberNotNull(nameof(database))]
        init {
            database = new(this);
            database.Database.EnsureCreated();

            // TODO Load only active ones
            foreach(var _ in database.Identities)
            {
                // Constructor auto-registers it
            }
            foreach(var account in database.Accounts)
            {
                accounts[account.Identifier] = account;
            }
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

    private Account CreateAccount(Identity identity, byte[] passwordHash)
    {
        var created = new Account(this, identity, passwordHash);
        database.Accounts.Add(created);
        return created;
    }

    private Identity GetIdentity(AccountName name, Func<AccountName, Identity> factory, out bool created)
    {
        try
        {
            var identity = identityByAccount.GetOrAdd(name, factory);
            created = identity == createdIdentity;
            if(created)
            {
                database.Identities.Add(identity);
            }
            return identity;
        }
        finally
        {
            createdIdentity = null;
        }
    }
}
