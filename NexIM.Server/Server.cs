namespace NexIM.Server;

public sealed partial class NexServer
{
    internal NexDatabase DatabaseConfig { get; }

    public NexServer(NexDatabase database)
    {
        DatabaseConfig = database;

        Database = default;
        Accounts = default;
        Authentication = default;
        Delivery = default;
    }
}

public abstract record NexDatabase
{
    public required string ConnectionString { get; init; }

    internal NexDatabase()
    {

    }

    public sealed record SQLite : NexDatabase
    {

    }
}
