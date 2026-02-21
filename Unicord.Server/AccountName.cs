using System;
using System.Diagnostics.CodeAnalysis;

namespace Unicord.Server;

public readonly struct AccountName : IEquatable<AccountName>
{
    public object? Identifier { get; }

    [MemberNotNullWhen(true, nameof(Identifier))]
    public bool IsValid => Identifier != null;

    private AccountName(object? identifier)
    {
        Identifier = identifier;
    }

    public static AccountName Get(object? identifier)
    {
        return new(identifier);
    }

    public bool Equals(AccountName other)
    {
        return Object.Equals(Identifier, other.Identifier);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is AccountName name ? Equals(name) : false;
    }

    public override int GetHashCode()
    {
        return Identifier?.GetHashCode() ?? 0;
    }

    public override string? ToString()
    {
        return Identifier?.ToString();
    }
}
