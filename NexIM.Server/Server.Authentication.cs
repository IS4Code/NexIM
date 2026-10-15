using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;
using NexIM.Server.Accounts.VCards;
using NexIM.Server.Authentication;
using NexIM.Server.Tools;

namespace NexIM.Server;

partial class NexServer
{
    private ValueTuple Authentication {
        init {

        }
    }

    public SaslSession? CreateSaslSession(string mechanismName, Func<string, AccountName> usernameResolver, INetworkSession networkSession)
    {
        return mechanismName switch {
            "ANONYMOUS" => new AnonymousSaslSession(this, usernameResolver, networkSession),
            "PLAIN" when networkSession.IsSecure => new PlainSaslSession(this, usernameResolver),
            "SCRAM-SHA-1" => new ScramSaslSession(this, usernameResolver, HashAlgorithmName.SHA1),
            "SCRAM-SHA-256" => new ScramSaslSession(this, usernameResolver, HashAlgorithmName.SHA256),
            _ => null
        };
    }

    public IEnumerable<string> GetSaslMechanisms(INetworkSession networkSession)
    {
        /*if(session.RemoteCertificate != null)
        {
            // Certificate could be used
            yield return "EXTERNAL";
        }*/
        // These do not transmit the password
        yield return "SCRAM-SHA-256";
        yield return "SCRAM-SHA-1";
        if(networkSession.IsSecure)
        {
            // Plaintext password requires a secure connection
            yield return "PLAIN";
        }
        yield return "ANONYMOUS";
    }

    public ValueTask<Account?> Authenticate(AccountName accountName, TemporaryString? password)
    {
        return AuthenticateAccount(accountName, password?.Value.AsMemory() ?? default, password);
    }

    internal async ValueTask<Account?> AuthenticateAccount(AccountName accountName, ReadOnlyMemory<char> password, IDisposable? memoryHandle)
    {
        if(!accountName.IsUser || password.Length == 0)
        {
            return null;
        }

        if(await FindIdentity(accountName) is not { Owned: true } identity)
        {
#if DEBUG
            // Auto-register when not registered already
            return await Register(accountName, password, memoryHandle, new("placeholder@example.org"), new());
#else
            return null;
#endif
        }

        try
        {
            if(await GetAccount(identity) is not { } account)
            {
                // TODO Database inconsistency (owned identity but no account)
                return null;
            }

            var result = await PasswordHasher.VerifyPassword(password, account.PasswordHash);
            if(result == PasswordHasher.VerificationResult.NotVerified)
            {
                // Password mismatch
                return null;
            }

            if(result == PasswordHasher.VerificationResult.VerifiedWeak)
            {
                // Give it a rehash
                account.PasswordHash = await PasswordHasher.HashPassword(password);
                await account.Save();
            }

            // Authenticated
            return account;
        }
        finally
        {
            memoryHandle?.Dispose();
        }
    }

    public async ValueTask<Account> AuthenticateAnonymous(Func<string, AccountName> usernameResolver, AccountName? assertedName, INetworkSession networkSession)
    {
        return CreateTemporaryAccount(usernameResolver, assertedName, networkSession);
    }

    public ValueTask<Account?> Register(AccountName accountName, TemporaryString password, MailAddress email, VCard vcard)
    {
        return Register(accountName, password.Value, password, email, vcard);
    }

    private async ValueTask<Account?> Register(AccountName accountName, ReadOnlyMemory<char> password, IDisposable? memoryHandle, MailAddress email, VCard vcard)
    {
        byte[] hash;
        try
        {
            if(!accountName.IsUser || password.Length == 0)
            {
                // TODO Status
                return null;
            }

            hash = await PasswordHasher.HashPassword(password);
        }
        finally
        {
            memoryHandle?.Dispose();
        }

        if(await CreateNewAccount(accountName, new(hash, email, vcard)) is not { } account)
        {
            // Already exists
            return null;
        }

        // Deduplication (prevent data race when the account is retrieved again)
        return await AddAccount(account);
    }
}
