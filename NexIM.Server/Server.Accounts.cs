using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NexIM.Server.Accounts;

namespace NexIM.Server;

partial class Server
{
    readonly ConcurrentDictionary<Guid, Account> accounts = new();
    readonly ConcurrentDictionary<Guid, Identity> identityByGuid = new();
    readonly ConcurrentDictionary<AccountName, Identity> identityByAccount = new();

    readonly Func<AccountName, Identity> createUnownedIdentity;
    readonly Func<AccountName, Identity> createOwnedIdentity;

    [ThreadStatic]
    static Identity? createdIdentity;

    private ValueTuple Accounts {
        [MemberNotNull(nameof(createUnownedIdentity))]
        [MemberNotNull(nameof(createOwnedIdentity))]
        init {
            createUnownedIdentity = name => {
                var identity = new Identity(IdentifierHelper.CreateGuid(name), name);
                identityByGuid[identity.Identifier] = identity;
                createdIdentity = identity;
                return identity;
            };

            createOwnedIdentity = name => {
                var identity = new Identity(IdentifierHelper.CreateGuid(), name);
                identityByGuid[identity.Identifier] = identity;
                createdIdentity = identity;
                return identity;
            };
        }
    }

    internal void RegisterIdentity(Identity identity)
    {
        // Called from DB
        identityByGuid[identity.Identifier] = identity;
        identityByAccount[identity.Name] = identity;
    }

    internal Identity GetAccountIdentity(AccountName name, out bool created)
    {
        return GetIdentity(name, createUnownedIdentity, out created);
    }

    internal Identity NewAccountIdentity(AccountName name, out bool created)
    {
        return GetIdentity(name, createOwnedIdentity, out created);
    }

    internal Identity? TryGetAccountIdentity(AccountName name)
    {
        return TryGetIdentity(name);
    }

    public Account? GetAccount(AccountName name)
    {
        // Does not create identity if non-existent
        return
            identityByAccount.TryGetValue(name, out var id)
            ? GetAccount(id) : null;
    }

    internal Account? GetAccount(Identity identity)
    {
        return
            accounts.TryGetValue(identity.Identifier, out var account)
            ? account : null;
    }
}
