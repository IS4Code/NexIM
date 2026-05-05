using System;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;

namespace NexIM.Server;

partial class Server
{
    private ValueTuple Authentication {
        init {

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
        if(!accountName.IsUser || password.Length == 0)
        {
            return null;
        }

        if(await FindIdentity(accountName) is not { Owned: true } identity)
        {
            // TODO Auto-register only in debug mode
            return await Register(accountName, password, memoryHandle, new("placeholder@example.org"), new());
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash(accountName, password, memoryHandle);

        if(await GetAccount(identity) is not { } account)
        {
            // TODO Database inconsistency (owned identity but no account)
            return null;
        }

        if(!CryptographicOperations.FixedTimeEquals(hash, account.PasswordHash))
        {
            // Password mismatch
            return null;
        }

        // Authenticated
        return account;
    }

    byte[] GetHash(AccountName accountName, ReadOnlyMemory<char> password, IDisposable? memoryHandle)
    {
        try
        {
            var buffer = new byte[SHA256.HashSizeInBytes];

            Span<byte> data = stackalloc byte[SHA256.HashSizeInBytes * 2];

            SHA256.HashData(MemoryMarshal.Cast<char, byte>(accountName.ToString()?.ToLowerInvariant()), data);
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

    public ValueTask<Account?> Register(AccountName accountName, TemporaryString password, MailAddress email, VCard vcard)
    {
        return Register(accountName, password.Value, password, email, vcard);
    }

    private async ValueTask<Account?> Register(AccountName accountName, ReadOnlyMemory<char> password, IDisposable? memoryHandle, MailAddress email, VCard vcard)
    {
        if(!accountName.IsUser || password.Length == 0)
        {
            // TODO Status
            return null;
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash(accountName, password, memoryHandle);

        if(await CreateNewAccount(accountName, new(hash, email, vcard)) is not { } account)
        {
            // Already exists
            return null;
        }

        // Deduplication (prevent data race when the account retrieved again)
        return await AddAccount(account);
    }
}
