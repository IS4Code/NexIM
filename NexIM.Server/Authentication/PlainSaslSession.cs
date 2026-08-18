using System;
using System.Threading.Tasks;
using NexIM.Primitives;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL session using the PLAIN mechanism.
/// </summary>
sealed class PlainSaslSession(NexServer server, Func<string, AccountName> usernameResolver) : SaslSession
{
    public override ValueTask<SaslResponse> Authenticate(TemporaryUtf8String? initialResponse) => Finish(initialResponse);

    public override ValueTask<SaslResponse> Continue(TemporaryUtf8String? response) => Finish(response);

    async ValueTask<SaslResponse> Finish(TemporaryUtf8String? data)
    {
        if(await server.AuthenticatePlain(data, usernameResolver) is not { } account)
        {
            return SaslResponse.Failure(SaslStatus.AuthenticationFailed);
        }
        return SaslResponse.Success(account);
    }
}
