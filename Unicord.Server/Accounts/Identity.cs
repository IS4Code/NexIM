using System;
using Unicord.Server.Database;

namespace Unicord.Server.Accounts;

internal sealed class Identity : IEquatable<Identity>
{
    public Guid Identifier { get; }
    public string? User { get; }
    public string Host { get; }

    public AccountName Name => new(User, Host);

    public Identity(Guid identifier, AccountName name)
    {
        Identifier = identifier;
        User = name.User;
        Host = name.Host;
    }

    public Identity(AccountsContext context, Guid identifier, string? user, string host) : this(identifier, new(user, host))
    {
        context.Server.RegisterIdentity(this);
    }

    public bool Equals(Identity? other)
    {
        return other?.Identifier.Equals(Identifier) ?? false;
    }

    public override bool Equals(object? obj)
    {
        return obj is Identity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Identifier.GetHashCode();
    }
}
