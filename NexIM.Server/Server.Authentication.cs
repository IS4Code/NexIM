using System;
using System.Diagnostics.CodeAnalysis;
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
    readonly Func<Guid, (Identity, byte[], MailAddress, VCard), Account> accountFactory;

    private ValueTuple Authentication {
        [MemberNotNull(nameof(accountFactory))]
        init {
            accountFactory = (_, info) => {
                var (id, hash, email, vcard) = info;
                return CreateAccount(id, hash, email, vcard);
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
        if(!accountName.IsUser || password.Length == 0)
        {
            return null;
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash(accountName, password, memoryHandle);

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
            // TODO Debug mode
            account = accounts.GetOrAdd(identity.Identifier, accountFactory, (identity, hash, new MailAddress("placeholder@example.org"), new VCard()));
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

    public async ValueTask<Account?> Register(AccountName accountName, TemporaryString password, MailAddress email, VCard vcard)
    {
        if(!accountName.IsUser || password.Length == 0)
        {
            // TODO Status
            return null;
        }

        var identity = NewAccountIdentity(accountName, out var created);

        if(!created)
        {
            // A pre-existing account
            return null;
        }

        // TODO Normal password hashing algorithm
        var hash = GetHash(accountName, password.Value, password);

        var account = accounts.GetOrAdd(identity.Identifier, accountFactory, (identity, hash, email, vcard));
        if(account.PasswordHash != hash)
        {
            // Created concurrently elsewhere
            return null;
        }

        // Just added
        await SaveDatabase();
        return account;
    }
}
