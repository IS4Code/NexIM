using System;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Unicord.Server.Accounts;
using Unicord.Server.Accounts.VCards;
using Unicord.Server.Events;

namespace Unicord.Server.Database;

internal class AccountsContext : DbContext
{
    public Server Server { get; }

    public DbSet<Account> Accounts { get; set; }

    static AccountsContext()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public AccountsContext(Server server)
    {
        Server = server;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=accounts.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e => {
            e.Ignore(x => x.Name);
            e.HasKey(x => new { x.Host, x.User });

            e.Property(x => x.PasswordHash);

            e.Property(x => x.VCard).HasConversion(
                x => SaveVCard(x),
                x => LoadVCard(x)
            );

            e.Ignore(x => x.Contacts);
            e.OwnsMany(x => x.ContactsBuilder, e => {
                e.Ignore(x => x.Account);

                e.HasKey(x => new { x.Host, x.User });

                e.Property(x => x.SubscriptionState).HasConversion(
                    x => (ushort)((byte)x.From | ((byte)x.To << 8)),
                    x => new((SubscriptionLevel)(x & 0xFF), (SubscriptionLevel)(x >> 8))
                );
            });

            e.Ignore(x => x.PrivateStorage);
            e.OwnsMany(x => x.PrivateStorageBuilder, e => {
                e.HasKey(x => new { x.KeyNamespace, x.KeyName });

                e.Property(x => x.Extensions).HasConversion(
                    x => SaveExtensions(x),
                    x => LoadExtensions(x)
                );
            });
        });
    }

    static readonly MessagePackSerializerOptions vcardSerializerOptions = MessagePackSerializerOptions.Standard;

    private static byte[]? SaveVCard(VCard? vcard)
    {
        if(vcard == null)
        {
            return null;
        }
        return MessagePackSerializer.Serialize(vcard, vcardSerializerOptions);
    }

    private static VCard? LoadVCard(byte[]? data)
    {
        if(data == null)
        {
            return null;
        }
        return MessagePackSerializer.Deserialize<VCard>(data, vcardSerializerOptions);
    }

    static readonly MessagePackSerializerOptions extensionsSerializerOptions = MessagePackSerializerOptions.Standard;

    private static byte[] SaveExtensions(EventExtensions extensions)
    {
        if(extensions.IsEmpty)
        {
            return Array.Empty<byte>();
        }
        return MessagePackSerializer.Serialize(extensions, extensionsSerializerOptions);
    }

    private static EventExtensions LoadExtensions(byte[]? data)
    {
        if(data is null or { Length: 0 })
        {
            return default;
        }
        return MessagePackSerializer.Deserialize<EventExtensions>(data, extensionsSerializerOptions);
    }
}
