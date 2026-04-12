namespace Unicord.Server.Events;

public enum ErrorCode
{
    NotAvailable,
    Success,
    InvalidRequest,
    NotFound,
    Unrecognized,
    // TODO Do not conflate with Forbidden
    NotAuthorized
}
