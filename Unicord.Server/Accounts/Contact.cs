using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;

namespace Unicord.Server.Accounts;

using static SubscriptionLevel;

public record Contact : IComparable<Contact>
{
    [NotMapped]
    public required AccountName Account { get; init; }

    public required SubscriptionState SubscriptionState { get; init; }
    public string? Name { get; init; }
    public string? Group { get; init; }

    internal string? User {
        get => Account.User;
        init => Account = Account with { User = value };
    }

    internal string Host {
        get => Account.Host;
        init => Account = Account with { Host = value };
    }

    internal Guid AccountIdentifier { get; init; }

    public Contact WithSubscriptionState(SubscriptionState newState)
    {
        if(SubscriptionState == newState)
        {
            // Already has this state
            return this;
        }
        return this with { SubscriptionState = newState };
    }

    int IComparable<Contact>.CompareTo(Contact? other)
    {
        if(other is null)
        {
            return 1;
        }
        return Account.CompareTo(other.Account);
    }
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct SubscriptionState(
    SubscriptionLevel From,
    SubscriptionLevel To
)
{
    public bool AcceptedFrom {
        get => (Accepted & From) != 0;
        init => From |= value ? Accepted : 0;
    }
    public bool AcceptedTo {
        get => (Accepted & To) != 0;
        init => To |= value ? Accepted : 0;
    }
    public bool ApprovedFrom {
        get => (Approved & From) != 0;
        init => From |= value ? Approved : 0;
    }
    public bool ApprovedTo {
        get => (Approved & To) != 0;
        init => To |= value ? Approved : 0;
    }
    public bool PendingFrom {
        get => (Pending & From) != 0;
        init => From |= value ? Pending : 0;
    }
    public bool PendingTo {
        get => (Pending & To) != 0;
        init => To |= value ? Pending : 0;
    }

    public SubscriptionDirection Direction {
        get =>
            ((From & Accepted) != 0 ? SubscriptionDirection.From : 0) |
            ((To & Accepted) != 0 ? SubscriptionDirection.To : 0);

        init {
            switch(value)
            {
                case SubscriptionDirection.To:
                    To |= Accepted;
                    break;
                case SubscriptionDirection.From:
                    From |= Accepted;
                    break;
                case SubscriptionDirection.Both:
                    To |= Accepted;
                    From |= Accepted;
                    break;
            }
        }
    }

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
