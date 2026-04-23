using System;
using System.Diagnostics.CodeAnalysis;
using Unicord.Server.Accounts;

namespace Unicord.Server;

public readonly record struct AccountName(string? User, string Host) : IComparable<AccountName>
{
    static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    [MemberNotNullWhen(true, nameof(User))]
    public bool IsValid => User != null;

    public Identifier ToIdentifier(string? resource = null) => new(this, resource);

    public int CompareTo(AccountName other)
    {
        // Host first (top-level hierarchy unit)
        int cmp = comparer.Compare(Host, other.Host);
        if(cmp != 0)
        {
            return cmp;
        }
        return comparer.Compare(User, other.User);
    }

    public override string? ToString()
    {
        return
            User is { } user
            ? $"{user}@{Host}"
            : Host;
    }

    public Uri ToUri()
    {
        if(User is not { } user)
        {
            // TODO DNS escaping with \
            return new Uri("dns:" + Uri.EscapeDataString(Host));
        }
        // TODO A-label normalization
        return new Uri($"acct:{Uri.EscapeDataString(user)}@{Uri.EscapeDataString(Host)}");
    }
}
