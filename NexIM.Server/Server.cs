using Microsoft.EntityFrameworkCore;

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

    public void Close()
    {
        DatabaseClose();
    }
}

public abstract record NexDatabase
{
    public required string ConnectionString { get; init; }

    internal NexDatabase()
    {

    }

    public abstract void Use(DbContextOptionsBuilder builder);

    public sealed record Sqlite : NexDatabase
    {
        public override void Use(DbContextOptionsBuilder builder)
        {
            builder.UseSqlite(ConnectionString);
        }
    }

    public sealed record MySQL : NexDatabase
    {
        public override void Use(DbContextOptionsBuilder builder)
        {
            builder.UseMySQL(ConnectionString);
        }
    }

    public sealed record PostgreSQL : NexDatabase
    {
        public override void Use(DbContextOptionsBuilder builder)
        {
            builder.UseNpgsql(ConnectionString);
        }
    }
}
