using System;
using System.Runtime.InteropServices;

namespace Unicord.Server.Model;

using static SubscriptionDirection;

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
    SubscriptionDirection Accepted,
    SubscriptionDirection Approved,
    SubscriptionDirection Pending
)
{
    public bool AcceptedFrom => (Accepted & From) != 0;
    public bool AcceptedTo => (Accepted & To) != 0;
    public bool ApprovedFrom => (Approved & From) != 0;
    public bool ApprovedTo => (Approved & To) != 0;
    public bool PendingFrom => (Pending & From) != 0;
    public bool PendingTo => (Pending & To) != 0;

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
    public SubscriptionState WithAcceptedFrom() => this with { Accepted = Accepted | From, Approved = (Approved & ~From) | To, Pending = Pending & ~From };

    /// <summary>
    /// Clears the "pending to" state and sets the "accepted/approved to" state.
    /// </summary>
    public SubscriptionState WithAcceptedTo() => this with { Accepted = Accepted | To, Approved = Approved | To, Pending = Pending & ~To };

    /// <summary>
    /// Clears any "accepted/pending from" state and sets the "approved from" and "approved to" state.
    /// </summary>
    public SubscriptionState WithApprovedFrom() => this with { Accepted = (Accepted & ~From) | To, Approved = Approved | From, Pending = Pending & ~From };

    /// <summary>
    /// Sets the "approved to" state.
    /// </summary>
    public SubscriptionState WithApprovedTo() => this with { Approved = Approved | To };

    /// <summary>
    /// Clears the "accepted/approved from" and sets the "pending from" state.
    /// </summary>
    public SubscriptionState WithPendingFrom() => this with { Accepted = Accepted & ~From, Approved = Approved & ~From, Pending = Pending | From };

    /// <summary>
    /// Clears the "accepted to" state and sets the "approved/pending to" state.
    /// </summary>
    public SubscriptionState WithPendingTo() => this with { Accepted = Accepted & ~To, Approved = Approved | To, Pending = Pending | To };

    /// <summary>
    /// Clears the "accepted/approved/pending from" state.
    /// </summary>
    public SubscriptionState WithoutFrom() => this with { Accepted = Accepted & ~From, Approved = Approved & ~From, Pending = Pending & ~From };

    /// <summary>
    /// Clears the "accepted/pending to" state.
    /// </summary>
    public SubscriptionState WithoutTo() => this with { Accepted = Accepted & ~To, Pending = Pending & ~To };
}

[Flags]
public enum SubscriptionDirection : byte
{
    None = 0,
    To = 1,
    From = 2,
    Both = 3
}
