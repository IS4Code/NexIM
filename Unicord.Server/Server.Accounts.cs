using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unicord.Server.Accounts;

namespace Unicord.Server;

partial class Server
{
    readonly ConcurrentDictionary<AccountName, Account> accounts = new();

    public async ValueTask<Account?> AuthenticateAccount(AccountName accountName, ReadOnlyMemory<char> password, IDisposable? memoryHandle)
    {
        if(!accountName.IsValid || password.Length == 0)
        {
            return null;
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash();

        // TODO Registration
        var account = accounts.GetOrAdd(accountName, _ => CreateAccount(accountName.User, accountName.Host, hash));
        if(account.PasswordHash == hash)
        {
            // Newly added
            await SaveDatabase();
            return account;
        }
        else if(CryptographicOperations.FixedTimeEquals(hash, account.PasswordHash))
        {
            return account;
        }
        return null;

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

    public Account? GetAccount(AccountName name)
    {
        return accounts.TryGetValue(name, out var account) ? account : null;
    }
}
