using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Server.Accounts;

namespace NexIM.Server;

partial class NexServer
{
    // TODO Cleanup if not referenced
    readonly ConcurrentDictionary<Guid, ValueTask<Account?>> accounts = new();

    readonly Func<Guid, ValueTask<Account?>> findAccountAddFactory;
    readonly Func<Guid, ValueTask<Account?>, ValueTask<Account?>> findAccountUpdateFactory;
    readonly Func<Guid, Account, ValueTask<Account>> addAccountAddFactory;
    readonly Func<Guid, ValueTask<Account?>, Account, ValueTask<Account>> addAccountUpdateFactory;

    private ValueTuple Accounts {
        [MemberNotNull(nameof(findAccountAddFactory), nameof(findAccountUpdateFactory), nameof(addAccountAddFactory), nameof(addAccountUpdateFactory))]
        init {
            findAccountAddFactory = guid => {
                return FindAccount(guid).Preserve();
            };

            findAccountUpdateFactory = (guid, previous) => {
                if(previous.IsCompleted && (!previous.IsCompletedSuccessfully || previous.GetAwaiter().GetResult() == null))
                {
                    // Completed without result - try again
                    return FindAccount(guid).Preserve();
                }
                // Use in progress or successful query
                return previous;
            };

            addAccountAddFactory = (guid, added) => {
                // First one added
                return new(added);
            };

            addAccountUpdateFactory = (guid, previous, added) => {
                if(previous.IsCompleted && (!previous.IsCompletedSuccessfully || previous.GetAwaiter().GetResult() == null))
                {
                    // Completed without result - use instance
                    return new(added);
                }
                // An in-progress query was exposed, which must be used if it returns something
                return new(Inner());
                async Task<Account> Inner()
                {
                    try
                    {
                        if(await previous is { } result)
                        {
                            // Forget the instance and use this result instead
                            return result;
                        }
                    }
                    catch
                    {
                        // Ignore - exception will be handled by the code that started the task
                    }
                    // No result - this one can be safely used
                    return added;
                }
            };
        }
    }

    public async ValueTask<Account?> GetAccount(AccountName name, CancellationToken cancellationToken = default)
    {
        if(await FindIdentity(name, cancellationToken) is not { } id)
        {
            return null;
        }
        return await GetAccount(id);
    }

    internal ValueTask<Account?> GetAccount(Identity id)
    {
        return accounts.AddOrUpdate(id.Identifier, findAccountAddFactory, findAccountUpdateFactory);
    }

    internal ValueTask<Account> AddAccount(Account account)
    {
        // The factories always produce a result
        return accounts.AddOrUpdate(account.Identifier, addAccountAddFactory!, addAccountUpdateFactory!, account)!;
    }
}
