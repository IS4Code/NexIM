using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unicord.Server.Tools;

namespace Unicord.Server;

public class AccountsManager
{
    // TODO Normal password hashing algorithm
    readonly ConcurrentDictionary<string, byte[]> hashes = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<bool> Authenticate(string? username, TemporaryString? password)
    {
        if(username == null || password == null)
        {
            return false;
        }

        var hash = GetHash();

        var existing = hashes.GetOrAdd(username, hash);
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

                SHA256.HashData(MemoryMarshal.Cast<char, byte>(username), data);
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
}
