using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;

namespace Unicord.Server.Accounts;

using static SubscriptionLevel;

public record Contact : IComparable<Contact>
{
    [NotMapped]
    public required AccountName Account { get; init; }

    public SubscriptionState SubscriptionState { get; init; }
    public string? Nickname { get; init; }
    public string? Group { get; init; }

    internal Identity Identity { get; init; } = null!;
    internal Guid Identifier { get; init; }
    internal Guid OwnerIdentifier { get; init; }

    /// <summary>
    /// Uses the data from a <see cref="Contact"/> instance
    /// to initialize a new owned contact.
    /// </summary>
    internal static Contact Create(Identity identity, Contact data, Account owner)
    {
        return data with {
            Account = identity.Name,
            Identity = identity,
            Identifier = identity.Identifier,
            OwnerIdentifier = owner.Identifier,
            SubscriptionState = default(SubscriptionState) with { ApprovedTo = data.SubscriptionState.ApprovedTo }
        };
    }

    /// <summary>
    /// Uses the data from a <see cref="Contact"/> instance
    /// to initialize a new owned contact.
    /// </summary>
    internal static Contact Create(Identity identity, SubscriptionState subscriptionState, Account owner)
    {
        return new Contact() {
            Account = identity.Name,
            Identity = identity,
            Identifier = identity.Identifier,
            OwnerIdentifier = owner.Identifier,
            SubscriptionState = subscriptionState
        };
    }

    /// <summary>
    /// Updates the <see cref="Contact"/> instance
    /// with new data.
    /// </summary>
    internal Contact Update(Contact other)
    {
        return other with {
            Account = Account,
            Identity = Identity,
            Identifier = Identifier,
            OwnerIdentifier = OwnerIdentifier,
            SubscriptionState = SubscriptionState with { ApprovedTo = other.SubscriptionState.ApprovedTo }
        };
    }

    // New contact's subscription state "approved to" flag is set only if the request originates from the user.

    internal Contact WithSubscriptionState(SubscriptionState newState)
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
        init => From = SetBit(From, Accepted, value);
    }
    public bool AcceptedTo {
        get => (Accepted & To) != 0;
        init => To = SetBit(To, Accepted, value);
    }
    public bool ApprovedFrom {
        get => (Approved & From) != 0;
        init => From = SetBit(From, Approved, value);
    }
    public bool ApprovedTo {
        get => (Approved & To) != 0;
        init => To = SetBit(To, Approved, value);
    }
    public bool PendingFrom {
        get => (Pending & From) != 0;
        init => From = SetBit(From, Pending, value);
    }
    public bool PendingTo {
        get => (Pending & To) != 0;
        init => To = SetBit(To, Pending, value);
    }

    public bool IsEmpty => From == 0 && To == 0;

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

    static SubscriptionLevel SetBit(SubscriptionLevel level, SubscriptionLevel bit, bool set)
    {
        return set ? (level | bit) : (level &= ~bit);
    }
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
