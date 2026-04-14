namespace Unicord.Server.Events;

public enum StatusCode
{
    NotAvailable,
    Success,
    InvalidRequest,
    NotFound,
    Unrecognized,
    // TODO Do not conflate with Forbidden
    NotAuthorized
}
