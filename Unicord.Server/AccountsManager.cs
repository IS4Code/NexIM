using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unicord.Server.Model;
using Unicord.Server.Primitives;

namespace Unicord.Server;

public class AccountsManager
{
    readonly ConcurrentDictionary<AccountName, Account> accounts = new();

    public AccountsManager()
    {

    }

    public async ValueTask<bool> Authenticate(AccountName accountName, TemporaryString? password)
    {
        if(!accountName.IsValid || password == null)
        {
            return false;
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash();

        var existing = accounts.GetOrAdd(accountName, _ => new Account(accountName, hash)).PasswordHash;
        if(existing == hash)
        {
            // Newly added
            return true;
        }
        else
        {
            return CryptographicOperations.FixedTimeEquals(hash, existing);
        }

        byte[] GetHash()
        {
            try
            {
                var buffer = new byte[SHA256.HashSizeInBytes];

                Span<byte> data = stackalloc byte[SHA256.HashSizeInBytes * 2];

                SHA256.HashData(MemoryMarshal.Cast<char, byte>(accountName.ToString()), data);
                SHA256.HashData(MemoryMarshal.Cast<char, byte>(password.Value), data.Slice(SHA256.HashSizeInBytes));
                SHA256.HashData(data, buffer);

                return buffer;
            }
            finally
            {
                // No longer needed
                password.Dispose();
            }
        }
    }

    public Account? GetAccount(AccountName name)
    {
        return accounts.TryGetValue(name, out var account) ? account : null;
    }
}
