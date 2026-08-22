using System;
using System.Collections.Generic;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL session using the PLAIN mechanism.
/// </summary>
sealed class PlainSaslSession(NexServer server, Func<string, AccountName> usernameResolver) : SaslSession
{
    protected async override IAsyncEnumerator<SaslResponse> Run()
    {
        if(await server.AuthenticatePlain(CurrentResponse, usernameResolver) is not { } account)
        {
            yield return SaslResponse.Failure(SaslStatus.AuthenticationFailed);
        }
        else
        {
            yield return SaslResponse.Success(account);
        }
    }
}
