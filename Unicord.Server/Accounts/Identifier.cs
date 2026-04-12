global using Identifiers = Unicord.Server.Tools.NonEmptySet<Unicord.Server.Accounts.Identifier>;
using System;
using System.Collections.Generic;

namespace Unicord.Server.Accounts;

public readonly record struct Identifier(AccountName? Account, string? Resource) : IComparable<Identifier>
{
    static readonly Comparer<AccountName?> accountComparer = Comparer<AccountName?>.Default;
    static readonly StringComparer resourceComparer = StringComparer.Ordinal;

    public static readonly Identifier Null = default;

    public Identifier Bare => new(Account, null);

    public int CompareTo(Identifier other)
    {
        // First by account, then resource
        int cmp = accountComparer.Compare(Account, other.Account);
        if(cmp != 0)
        {
            return cmp;
        }
        return resourceComparer.Compare(Resource, other.Resource);
    }

    public override string? ToString()
    {
        return
            Account is { } acc
            ? Resource is { } res
            ? $"{acc}/{res}"
            : acc.ToString()
            : Resource?.ToString();
    }
}
