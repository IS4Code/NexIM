namespace Unicord.Server.Accounts;

public readonly record struct Identifier(AccountName? Account, string? Resource)
{
    public static readonly Identifier Null = default;

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
