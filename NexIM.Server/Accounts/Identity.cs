using System;
using NexIM.Server.Database;

namespace NexIM.Server.Accounts;

internal sealed class Identity : IEquatable<Identity>
{
    public Guid Identifier { get; }
    public string? User { get; }
    public string Host { get; }

    public bool Owned {
        get => Identifier.Version switch {
            5 => false,
            7 => true,
            _ => throw new InvalidOperationException("Only version 5 or 7 UUIDs are expected.")
        };
        private set {
            // Set from DB
            if(value != Owned)
            {
                throw new ArgumentException("The property value must correspond to the version of the UUID.", nameof(value));
            }
        }
    }

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
