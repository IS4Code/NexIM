namespace NexIM.Server;

public sealed partial class NexServer
{
    internal string SQLiteConnectionString { get; }

    public NexServer(string sqliteConnectionString)
    {
        SQLiteConnectionString = sqliteConnectionString;

        Database = default;
        Accounts = default;
        Authentication = default;
        Delivery = default;
    }
}
