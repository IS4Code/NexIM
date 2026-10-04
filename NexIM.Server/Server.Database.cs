using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;
using NexIM.Server.Database;
using NexIM.Tools;

namespace NexIM.Server;

partial class NexServer
{
    /// <summary>
    /// The shared database context used for lookups of entities.
    /// The only mutation that happens through this context is for
    /// adding unowned identities.
    /// </summary>
    readonly GlobalContext globalDatabase;
    // TODO Possibly pool these contexts to establish multiple connections when the lock becomes a bottleneck

    /// <summary>
    /// Used to keep track of per-account contexts before collection.
    /// </summary>
    readonly ConditionalWeakTable<AccountContext, object?> accountContexts = new();

    private ValueTuple Database {
        [MemberNotNull(nameof(globalDatabase))]
        init {
            globalDatabase = new(this);
            globalDatabase.Database.EnsureCreated();
        }
    }

    private void DatabaseClose()
    {
        globalDatabase.Dispose();

        foreach(var pair in accountContexts)
        {
            var context = pair.Key;
            context.Dispose();
        }

        if(DatabaseConfig is NexDatabase.Sqlite)
        {
            // Clear pools for past connections
            SqliteConnection.ClearAllPools();
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
        accountContexts.Add(context, null);

        return await context.FullAccounts.FirstOrDefaultAsync(x => x.Identifier == identityGuid, cancellationToken);
    }

    internal async ValueTask<Account?> CreateNewAccount(AccountName name, AccountRegistrationInfo info, CancellationToken cancellationToken = default)
    {
        // Work in an isolated context to ensure everything happens as a transaction
        var context = new AccountContext(this);
        accountContexts.Add(context, null);

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

    private Account CreateTemporaryAccount(Func<string, AccountName> usernameResolver, AccountName? assertedName, INetworkSession networkSession)
    {
        var context = new AccountContext(this);
        accountContexts.Add(context, null);

        // Use GUID in account name
        var identifier = IdentifierHelper.CreateGuid(out var timestamp);
        var identity = new Identity(identifier, usernameResolver(identifier.ToString("N")));

        // Not added to the DB context
        return new Account(context, identity, Array.Empty<byte>()) {
            Created = timestamp,
            Email = GetDefaultEmail(assertedName, networkSession),
            VCard = new()
        };
    }

    const string unknownUser = "unknown";
    const string invalidHost = "invalid";
    static readonly MailAddress unknownEmail = new($"{unknownUser}@{invalidHost}");
    static readonly SearchValues<char> invalidAddressChars = SearchValues.Create(" \t\r\n\"'<>()[]\\,:;@*");
    private static MailAddress GetDefaultEmail(AccountName? accountName, INetworkSession networkSession)
    {
        if(accountName?.User is not { Length: > 0 } user || user.AsSpan().ContainsAny(invalidAddressChars))
        {
            // Bad user
            user = unknownUser;
        }
        if(accountName?.Host is not { Length: > 0 } host || host.AsSpan().ContainsAny(invalidAddressChars))
        {
            // Bad host
            host = networkSession.RemoteEndPoint.GetHostName();

            if(host is not { Length: > 0 } || host.AsSpan().ContainsAny(invalidAddressChars))
            {
                host = invalidHost;
            }
        }

        try
        {
            return new MailAddress($"{user}@{host}");
        }
        catch(FormatException)
        {
            return unknownEmail;
        }
    }

    internal readonly record struct AccountRegistrationInfo(byte[] PasswordHash, MailAddress Email, VCard VCard);
}
