global using StatusReports = Unicord.Server.Tools.NonEmptySet<Unicord.Server.Events.StatusReport>;
using System;
using System.Runtime.InteropServices;
using Unicord.Server.Accounts;

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

[StructLayout(LayoutKind.Auto)]
public readonly record struct StatusReport(Identifier Source, StatusCode Code) : IComparable<StatusReport>
{
    public int CompareTo(StatusReport other)
    {
        int cmp = Source.CompareTo(other.Source);
        if(cmp != 0)
        {
            return cmp;
        }
        return ((int)Code).CompareTo((int)other.Code);
    }
}
