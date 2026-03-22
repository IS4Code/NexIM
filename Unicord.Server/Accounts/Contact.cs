using System;
using System.Runtime.InteropServices;

namespace Unicord.Server.Accounts;

using static SubscriptionLevel;

public record Contact(
    AccountName Account,
    SubscriptionState SubscriptionState,
    string? Name = null,
    string? Group = null
)
{
    public Contact WithSubscriptionState(SubscriptionState newState)
    {
        if(SubscriptionState == newState)
        {
            // Already has this state
            return this;
        }
        return this with { SubscriptionState = newState };
    }
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct SubscriptionState(
    SubscriptionLevel From,
    SubscriptionLevel To
)
{
    public bool AcceptedFrom => (Accepted & From) != 0;
    public bool AcceptedTo => (Accepted & To) != 0;
    public bool ApprovedFrom => (Approved & From) != 0;
    public bool ApprovedTo => (Approved & To) != 0;
    public bool PendingFrom => (Pending & From) != 0;
    public bool PendingTo => (Pending & To) != 0;

    private SubscriptionDirection GetDirection(SubscriptionLevel level)
    {
        return
            ((From & level) != 0 ? SubscriptionDirection.From : 0) |
            ((To & level) != 0 ? SubscriptionDirection.To : 0);
    }

    public SubscriptionDirection Direction => GetDirection(Accepted);

    /// <summary>
    /// The initial state of a new contact added by the user.
    /// </summary>
    public static readonly SubscriptionState Initial = default;

    /// <summary>
    /// The initial state of a new contact added by the user.
    /// </summary>
    public static readonly SubscriptionState InitialApprovedTo = Initial.WithApprovedTo();

    /// <summary>
    /// The initial state of a new contact added by the user approving subscription from.
    /// </summary>
    public static readonly SubscriptionState InitialApprovedFrom = Initial.WithApprovedFrom();

    /// <summary>
    /// The initial state of a new contact created by sending a subscription request.
    /// </summary>
    public static readonly SubscriptionState InitialPendingTo = Initial.WithPendingTo();

    /// <summary>
    /// The initial state of a new contact created by receiving a subscription request.
    /// </summary>
    public static readonly SubscriptionState InitialPendingFrom = Initial.WithPendingFrom();

    /// <summary>
    /// Clears any "approved/pending from" state and sets the "accepted from" and "approved to" state.
    /// </summary>
    public SubscriptionState WithAcceptedFrom() => this with { From = Accepted, To = To | Approved };

    /// <summary>
    /// Clears the "pending to" state and sets the "accepted/approved to" state.
    /// </summary>
    public SubscriptionState WithAcceptedTo() => this with { To = Accepted | Approved };

    /// <summary>
    /// Clears any "accepted/pending from" state and sets the "approved from" and "approved to" state.
    /// </summary>
    public SubscriptionState WithApprovedFrom() => this with { From = Approved, To = To | Approved };

    /// <summary>
    /// Sets the "approved to" state.
    /// </summary>
    public SubscriptionState WithApprovedTo() => this with { To = To | Approved };

    /// <summary>
    /// Clears the "accepted/approved from" and sets the "pending from" state.
    /// </summary>
    public SubscriptionState WithPendingFrom() => this with { From = Pending };

    /// <summary>
    /// Clears the "accepted to" state and sets the "approved/pending to" state.
    /// </summary>
    public SubscriptionState WithPendingTo() => this with { To = Approved | Pending };

    /// <summary>
    /// Clears the "accepted/approved/pending from" state.
    /// </summary>
    public SubscriptionState WithoutFrom() => this with { From = 0 };

    /// <summary>
    /// Clears the "accepted/pending to" state.
    /// </summary>
    public SubscriptionState WithoutTo() => this with { To = To & Approved };
}

[Flags]
public enum SubscriptionLevel : byte
{
    None = 0,
    Approved = 1,
    Pending = 2,
    Accepted = 4
}

[Flags]
public enum SubscriptionDirection : byte
{
    None = 0,
    To = 1,
    From = 2,
    Both = 3
}
