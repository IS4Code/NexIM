using System;
using System.Collections.Generic;
using NexIM.Primitives;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL session using the ANONYMOUS mechanism.
/// </summary>
sealed class AnonymousSaslSession(NexServer server, Func<string, AccountName> usernameResolver, INetworkSession networkSession) : SaslSession
{
    protected async override IAsyncEnumerator<SaslResponse> Run()
    {
        var assertedName = GetAssertedName(CurrentResponse);
        var account = await server.AuthenticateAnonymous(usernameResolver, assertedName, networkSession);
        yield return SaslResponse.Success(account);
    }

    const int maxTraceLength = 255;

    private AccountName? GetAssertedName(TemporaryUtf8String? trace)
    {
        if(trace == null || trace.Length > maxTraceLength)
        {
            // No information
            return null;
        }

        var text = trace.Value.AsMemory();
        int hostAt = text.Span.IndexOf('@');

        if(hostAt == -1)
        {
            return new(text.ToString(), "");
        }

        return new(text.Slice(0, hostAt).ToString(), text.Slice(hostAt + 1).ToString());
    }
}
