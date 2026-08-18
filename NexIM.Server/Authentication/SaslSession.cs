using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL negotiation session.
/// </summary>
public abstract class SaslSession : IAsyncDisposable
{
    public abstract ValueTask<SaslResponse> Authenticate(TemporaryUtf8String? initialResponse);

    public abstract ValueTask<SaslResponse> Continue(TemporaryUtf8String? response);

    public virtual ValueTask DisposeAsync() => default;
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
    public static SaslResponse Failure(SaslStatus reason) => new(reason, null, null);
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
