using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL negotiation session.
/// </summary>
public abstract class SaslSession : IAsyncDisposable
{
    IAsyncEnumerator<SaslResponse>? state;

    protected TemporaryUtf8String? CurrentResponse { get; private set; }

    protected abstract IAsyncEnumerator<SaslResponse> Run();

    public async ValueTask<SaslResponse> Response(TemporaryUtf8String? response)
    {
        try
        {
            CurrentResponse = response;
            state ??= Run();
            if(!await state.MoveNextAsync())
            {
                // Produced no response
                return SaslResponse.Failure();
            }
            return state.Current;
        }
        finally
        {
            response?.Dispose();
        }
    }

    public virtual ValueTask DisposeAsync() => state?.DisposeAsync() ?? default;
}

public readonly struct SaslResponse
{
    public SaslStatus Status { get; }
    public TemporaryUtf8String? ChallengeData { get; }
    public Account? Account { get; }

    private SaslResponse(SaslStatus status, TemporaryUtf8String? data, Account? account)
    {
        Status = status;
        ChallengeData = data;
        Account = account;
    }

    public static SaslResponse Challenge(TemporaryUtf8String? data) => new(SaslStatus.Challenge, data, null);
    public static SaslResponse Success(Account account, TemporaryUtf8String? data = null) => new(SaslStatus.Success, data, account);
    public static SaslResponse Failure(SaslStatus reason = SaslStatus.AuthenticationFailed) => new(reason, null, null);
}

public enum SaslStatus
{
    Challenge,
    Success,
    AuthenticationFailed,
    AuthorizationFailed,
    AccountDisabled,
    CredentialsExpired,
    TransportNotSecure,
    MechanismNotSupported,
    InvalidRequest,
    MechanismTooWeak,
    InternalError
}
