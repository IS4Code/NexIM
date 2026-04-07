namespace Unicord.Server;

public readonly record struct AccountName(string? User, string Host)
{
    public bool IsValid => User != null;

    public override string? ToString()
    {
        return
            User is { } user
            ? $"{user}@{Host}"
            : Host;
    }
}
