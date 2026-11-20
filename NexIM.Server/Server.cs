using System;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NexIM.Server.Tools;

namespace NexIM.Server;

public sealed partial class NexServer
{
    internal NexDatabase DatabaseConfig { get; }

    public string ProductName { get; } = typeof(NexServer).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product;
    public string ProductVersion { get; } = typeof(NexServer).Assembly.GetName().Version!.ToString(3);
    public DateTime StartDate { get; } = DateTime.UtcNow;
    public ICryptoProvider CryptoProvider { get; set; } = DefaultCryptoProvider.Instance;

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

sealed file class DefaultCryptoProvider : ICryptoProvider
{
    public static readonly DefaultCryptoProvider Instance = new();

    private DefaultCryptoProvider()
    {

    }

    public void RandomFill(Span<byte> data)
    {
        RandomNumberGenerator.Fill(data);
    }
}
