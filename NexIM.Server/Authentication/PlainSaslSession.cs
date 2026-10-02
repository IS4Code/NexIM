using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL session using the PLAIN mechanism.
/// </summary>
sealed class PlainSaslSession(NexServer server, Func<string, AccountName> usernameResolver) : SaslSession
{
    protected async override IAsyncEnumerator<SaslResponse> Run()
    {
        if(await Authenticate(CurrentResponse) is not { } account)
        {
            yield return SaslResponse.Failure(SaslStatus.AuthenticationFailed);
        }
        else
        {
            yield return SaslResponse.Success(account);
        }
    }

    private ValueTask<Account?> Authenticate(TemporaryUtf8String? response)
    {
        if(response == null)
        {
            return default;
        }

        var memory = response.Value.AsMemory();

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
        if(authzid.Length != 0 && !accountName.Matches(authzid.Span))
        {
            return default;
        }

        return server.AuthenticateAccount(accountName, password, response);
    }
}
