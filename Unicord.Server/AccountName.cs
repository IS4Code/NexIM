using System;

namespace Unicord.Server;

public readonly record struct AccountName(object? Identifier)
{
    public bool Equals(AccountName other)
    {
        return Object.Equals(Identifier, other.Identifier);
    }

    public override int GetHashCode()
    {
        return Identifier?.GetHashCode() ?? 0;
    }
}
