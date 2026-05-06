using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;
using NexIM.Server.Database;

namespace NexIM.Server;

partial class Server
{
    /// <summary>
    /// The shared database context used for lookups of entities.
    /// The only mutation that happens through this context is for
    /// adding unowned identities.
    /// </summary>
    readonly GlobalContext globalDatabase;
    // TODO Possibly pool these contexts to establish multiple connections when the lock becomes a bottleneck

    private ValueTuple Database {
        [MemberNotNull(nameof(globalDatabase))]
        init {
            globalDatabase = new(this);
            globalDatabase.Database.EnsureCreated();
        }
    }

    /// <summary>
    /// Performs a lookup for a previously uploaded file by its identifier.
    /// </summary>
    internal async ValueTask<UploadedFile?> FindUploadedFile(Guid identifier, CancellationToken cancellationToken = default)
    {
        await globalDatabase.Lock.WaitAsync(cancellationToken);
        try
        {
            return await globalDatabase.UploadedFiles.FindAsync(new object[] { identifier }, cancellationToken);
        }
        finally
        {
            globalDatabase.Lock.Release();
        }
    }

    /// <summary>
    /// Performs a lookup for a previously uploaded file by its SHA-1 hash.
    /// </summary>
    internal async ValueTask<UploadedFile?> FindUploadedFileBySha1(ArraySegment<byte> hash, CancellationToken cancellationToken = default)
    {
        await globalDatabase.Lock.WaitAsync(cancellationToken);
        try
        {
            return await globalDatabase.UploadedFiles.FirstOrDefaultAsync(x => x.Sha1Hash.SequenceEqual(hash), cancellationToken);
        }
        finally
        {
            globalDatabase.Lock.Release();
        }
    }

    /// <summary>
    /// Performs a lookup for an identity based on account name.
    /// </summary>
    /// <remarks>
    /// Pre-existing owned identities are preferred.
    /// </remarks>
    internal async ValueTask<Identity?> FindIdentity(AccountName name, CancellationToken cancellationToken = default)
    {
        await globalDatabase.Lock.WaitAsync(cancellationToken);
        try
        {
            return await (
                from id in globalDatabase.Identities
                where id.User == name.User && id.Host == name.Host
                orderby id.Owned descending
                select id
            ).FirstOrDefaultAsync(cancellationToken);
        }
        finally
        {
            globalDatabase.Lock.Release();
        }
    }

    /// <summary>
    /// Performs a lookup for an identity based on account name, or creates
    /// a new unowned one.
    /// </summary>
    /// <remarks>
    /// Pre-existing owned identities are preferred.
    /// </remarks>
    internal async ValueTask<Identity> FindOrCreateIdentity(AccountName name, CancellationToken cancellationToken = default)
    {
        await globalDatabase.Lock.WaitAsync(cancellationToken);
        try
        {
            var identity = await (
                from id in globalDatabase.Identities
                where id.User == name.User && id.Host == name.Host
                orderby id.Owned descending
                select id
            ).FirstOrDefaultAsync(cancellationToken);
            if(identity == null)
            {
                // Not found, create an unowned one
                identity = new(IdentifierHelper.CreateGuid(name), name);
                globalDatabase.Add(identity);
                try
                {
                    await globalDatabase.SaveChangesAsync(cancellationToken);
                }
                catch(DbUpdateException)
                {
                    // Unique constraint hit, the identity was added concurrently
                }
            }
            return identity;
        }
        finally
        {
            globalDatabase.Lock.Release();
        }
    }

    /// <summary>
    /// Performs a lookup for an identity based on account name.
    /// </summary>
    /// <remarks>
    /// Pre-existing owned identities are preferred.
    /// </remarks>
    internal async ValueTask<Identities?> FindIdentities(AccountNames names, CancellationToken cancellationToken = default)
    {
        if(names.TryGetSingle(out var singleName))
        {
            if(await FindIdentity(singleName, cancellationToken) is { } id)
            {
                return id;
            }
            return null;
        }

        var (users, hosts) = DeconstructAccountNames(names);
        var resultBuilder = Identities.Builder.Empty;
        var retrievedBuilder = AccountNames.Builder.Empty;

        await globalDatabase.Lock.WaitAsync(cancellationToken);
        try
        {
            await (
                from id in globalDatabase.Identities
                where users.Contains(id.User) && hosts.Contains(id.Host)
                orderby id.Owned descending
                select id
            ).ForEachAsync(x => {
                if(!names.Contains(x.Name))
                {
                    // Not an exact match
                    return;
                }
                if(!retrievedBuilder.Add(x.Name))
                {
                    // Already present (owned over unowned)
                    return;
                }
                resultBuilder.Add(x);
            }, cancellationToken);
        }
        finally
        {
            globalDatabase.Lock.Release();
        }

        return resultBuilder.TryToSet();
    }

    /// <summary>
    /// Performs a lookup for an identity based on account name, or creates
    /// a new unowned one.
    /// </summary>
    /// <remarks>
    /// Pre-existing owned identities are preferred.
    /// </remarks>
    internal async ValueTask<Identities> FindOrCreateIdentities(AccountNames names, CancellationToken cancellationToken = default)
    {
        if(names.TryGetSingle(out var singleName))
        {
            return await FindOrCreateIdentity(singleName, cancellationToken);
        }

        var (users, hosts) = DeconstructAccountNames(names);
        var resultBuilder = Identities.Builder.Empty;
        var retrievedBuilder = AccountNames.Builder.Empty;
        var remainingBuilder = names.ToBuilder();

        await globalDatabase.Lock.WaitAsync(cancellationToken);
        try
        {
            await (
                from id in globalDatabase.Identities
                where users.Contains(id.User) && hosts.Contains(id.Host)
                orderby id.Owned descending
                select id
            ).ForEachAsync(x => {
                if(!names.Contains(x.Name))
                {
                    // Not an exact match
                    return;
                }
                if(!retrievedBuilder.Add(x.Name))
                {
                    // Already present (owned over unowned)
                    return;
                }
                resultBuilder.Add(x);
                remainingBuilder.Remove(x.Name);
            }, cancellationToken);

            if(remainingBuilder.Count > 0)
            {
                foreach(var name in remainingBuilder)
                {
                    // Not found, create an unowned one
                    Identity identity = new(IdentifierHelper.CreateGuid(name), name);
                    resultBuilder.Add(identity);

                    globalDatabase.Add(identity);
                }

                try
                {
                    await globalDatabase.SaveChangesAsync(cancellationToken);
                }
                catch(DbUpdateException)
                {
                    // Unique constraint hit, the identity was added concurrently
                }
            }
        }
        finally
        {
            globalDatabase.Lock.Release();
        }

        return resultBuilder.TryToSet() ?? throw new InvalidOperationException("The builder is empty.");
    }

    private (HashSet<string?> users, HashSet<string> hosts) DeconstructAccountNames(AccountNames names)
    {
        var users = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in names)
        {
            users.Add(name.User);
            hosts.Add(name.Host);
        }
        return (users, hosts);
    }

    // TODO Recycle contexts

    internal async ValueTask<Account?> FindAccount(Guid identityGuid, CancellationToken cancellationToken = default)
    {
        // Prepare an isolated context for the account
        var context = new AccountContext(this);

        return await context.FullAccounts.FirstOrDefaultAsync(x => x.Identifier == identityGuid, cancellationToken);
    }

    internal async ValueTask<Account?> CreateNewAccount(AccountName name, AccountRegistrationInfo info, CancellationToken cancellationToken = default)
    {
        // Work in an isolated context to ensure everything happens as a transaction
        var context = new AccountContext(this);

        // Identity from the timestamp
        var identity = new Identity(IdentifierHelper.CreateGuid(out var timestamp), name);
        context.Add(identity);

        var account = new Account(context, identity, info.PasswordHash) {
            Created = timestamp,
            Email = info.Email,
            VCard = info.VCard
        };

        context.Add(account);

        try
        {
            // Write out the new account
            await context.SaveChangesAsync(cancellationToken);
        }
        catch(DbUpdateException)
        {
            // Can't create (likely already exists)
            return null;
        }

        return account;
    }

    internal readonly record struct AccountRegistrationInfo(byte[] PasswordHash, MailAddress Email, VCard VCard);
}
