using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server;

partial class Server
{
    readonly Func<Guid, (Identity, byte[]), Account> accountFactory;

    private ValueTuple Authentication {
        [MemberNotNull(nameof(accountFactory))]
        init {
            accountFactory = (_, info) => {
                var (id, hash) = info;
                return CreateAccount(id, hash);
            };
        }
    }

    public ValueTask<Account?> Authenticate(AccountName accountName, TemporaryString? password)
    {
        return AuthenticateAccount(accountName, password?.Value.AsMemory() ?? default, password);
    }

    public ValueTask<Account?> AuthenticatePlain(TemporaryUtf8String? data, Func<string, AccountName> usernameResolver)
    {
        if(data == null)
        {
            return default;
        }

        var memory = data.Value.AsMemory();

        // Format [authzid]NUL[authid]NUL[password]
        int usernameAt = memory.Span.IndexOf('\0');
        if(++usernameAt == 0)
        {
            return default;
        }
        int passwordAt = memory.Span.Slice(usernameAt).IndexOf('\0');
        if(++passwordAt == 0)
        {
            return default;
        }
        passwordAt += usernameAt;
        if(memory.Span.Slice(passwordAt).IndexOf('\0') != -1)
        {
            return default;
        }

        var authzid = memory.Slice(0, usernameAt - 1);
        var username = memory.Slice(usernameAt, passwordAt - usernameAt - 1).ToString();
        var password = memory.Slice(passwordAt);

        var accountName = usernameResolver(username);
        if(authzid.Length != 0 && !((ReadOnlySpan<char>)authzid.Span).Equals(accountName.ToString().AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        return AuthenticateAccount(accountName, password, data);
    }

    private async ValueTask<Account?> AuthenticateAccount(AccountName accountName, ReadOnlyMemory<char> password, IDisposable? memoryHandle)
    {
        if(!accountName.IsValid || password.Length == 0)
        {
            return null;
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash();

        var identity = NewAccountIdentity(accountName, out var created);
        Account? account;
        if(!created)
        {
            // A pre-existing account
            account = GetAccount(identity);
            if(account == null)
            {
                // TODO Account not preloaded or concurrently created?
                return null;
            }
        }
        else
        {
            // TODO Registration
            account = accounts.GetOrAdd(identity.Identifier, accountFactory, (identity, hash));
            if(account.PasswordHash == hash)
            {
                // Just added
                await SaveDatabase();
                return account;
            }
            // Concurrently added; password must still be verified
        }

        if(!CryptographicOperations.FixedTimeEquals(hash, account.PasswordHash))
        {
            // Password mismatch
            return null;
        }
        // Authenticated
        return account;

        byte[] GetHash()
        {
            try
            {
                var buffer = new byte[SHA256.HashSizeInBytes];

                Span<byte> data = stackalloc byte[SHA256.HashSizeInBytes * 2];

                SHA256.HashData(MemoryMarshal.Cast<char, byte>(accountName.ToString()), data);
                SHA256.HashData(MemoryMarshal.Cast<char, byte>(password.Span), data.Slice(SHA256.HashSizeInBytes));
                SHA256.HashData(data, buffer);

                return buffer;
            }
            finally
            {
                // No longer needed
                memoryHandle?.Dispose();
            }
        }
    }
}
