using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;
using NexIM.Server.Database;

namespace NexIM.Server;

partial class Server
{
    readonly SemaphoreSlim databaseSemaphore = new(1, 1);
    readonly AccountsContext database;

    private ValueTuple Database {
        [MemberNotNull(nameof(database))]
        init {
            database = new(this);
            database.Database.EnsureCreated();

            // TODO Load only active ones
            foreach(var account in database.FullAccounts)
            {
                accounts[account.Identifier] = account;
            }
        }
    }

    internal async Task SaveDatabase()
    {
        await databaseSemaphore.WaitAsync();
        try
        {
            await database.SaveChangesAsync();
        }
        finally
        {
            databaseSemaphore.Release();
        }
    }

    internal void AddUploadedFile(UploadedFile file)
    {
        database.UploadedFiles.Add(file);
    }

    internal UploadedFile? FindUploadedFile(Guid identifier)
    {
        return database.UploadedFiles.Find(identifier);
    }

    private Account CreateAccount(Identity identity, byte[] passwordHash, MailAddress email, VCard vcard)
    {
        var created = new Account(this, identity, passwordHash) {
            Email = email,
            VCard = vcard
        };
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

    private Identity? TryGetIdentity(AccountName name)
    {
        return identityByAccount.TryGetValue(name, out var identity) ? identity : null;
    }
}
