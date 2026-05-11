namespace NexIM.Server;

public sealed partial class Server
{
    internal string SQLiteConnectionString { get; }

    public Server(string sqliteConnectionString)
    {
        SQLiteConnectionString = sqliteConnectionString;

        Database = default;
        Accounts = default;
        Authentication = default;
        Delivery = default;
    }
}
