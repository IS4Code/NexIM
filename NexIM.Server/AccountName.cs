using System;
using System.Diagnostics.CodeAnalysis;
using NexIM.Server.Accounts;

namespace NexIM.Server;

public readonly struct AccountName : IEquatable<AccountName>, IComparable<AccountName>
{
    readonly string? _host;

    public string? User { get; }
    public string Host => _host ?? "";

    static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    [MemberNotNullWhen(true, nameof(User))]
    public bool IsValid => User != null;

    public bool IsLocal => Host == "";

    public static readonly AccountName Local = default;

    public AccountName(string? user, string host)
    {
        User = user;
        _host = host;
    }

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

    public bool Equals(AccountName other)
    {
        return
            comparer.Equals(User, other.User) &&
            comparer.Equals(Host, other.Host);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is AccountName other && Equals(other);
    }

    public override int GetHashCode()
    {
        var code = new HashCode();
        code.Add(User, comparer);
        code.Add(Host, comparer);
        return code.ToHashCode();
    }

    public static bool operator ==(AccountName a, AccountName b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(AccountName a, AccountName b)
    {
        return !a.Equals(b);
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
