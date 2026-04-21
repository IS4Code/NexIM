using System;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;
using Unicord.Primitives;
using Unicord.Server.Accounts;
using Unicord.Server.Accounts.VCards;
using Unicord.Server.Events;

namespace Unicord.Server.Database;

internal class AccountsContext : DbContext
{
    public Server Server { get; }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }

    readonly MessagePackSerializerOptions serializerOptions;

    static AccountsContext()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public AccountsContext(Server server)
    {
        Server = server;
        serializerOptions = CreateOptions(MessagePackSerializerOptions.Standard);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
#if DEBUG
        options.EnableSensitiveDataLogging();
#endif
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

                e.Property(x => x.Language).HasConversion(
                    x => SaveLanguage(x),
                    x => LoadLanguage(x)
                );

                e.Property(x => x.Data).HasConversion(
                    x => SaveExtensions(x),
                    x => LoadExtensions(x)
                );
            });

            e.Ignore(x => x.UploadedFiles);
            e.HasMany(x => x.UploadedFilesBuilder);
        });

        modelBuilder.Entity<UploadedFile>(e => {
            e.HasKey(x => x.Identifier);

            e.Property(x => x.Sha1Hash);
            e.Property(x => x.Sha256Hash);
        });
    }

    private string? SaveLanguage(LanguageCode? language)
    {
        return language?.Value;
    }

    private LanguageCode? LoadLanguage(string? language)
    {
        return language != null ? new LanguageCode(language) : null;
    }

    private byte[]? SaveVCard(VCard? vcard)
    {
        if(vcard == null)
        {
            return null;
        }
        return MessagePackSerializer.Serialize(vcard, serializerOptions);
    }

    private VCard? LoadVCard(byte[]? data)
    {
        if(data == null)
        {
            return null;
        }
        return MessagePackSerializer.Deserialize<VCard>(data, serializerOptions);
    }

    private byte[] SaveExtensions(EventExtensions extensions)
    {
        if(extensions.IsEmpty)
        {
            return Array.Empty<byte>();
        }
        return MessagePackSerializer.Serialize(extensions, serializerOptions);
    }

    private EventExtensions LoadExtensions(byte[]? data)
    {
        if(data is null or { Length: 0 })
        {
            return default;
        }
        return MessagePackSerializer.Deserialize<EventExtensions>(data, serializerOptions);
    }

    MessagePackSerializerOptions CreateOptions(MessagePackSerializerOptions from)
    {
        return from.WithResolver(
            CompositeResolver.Create(
                from.Resolver,
                new Resolver(from.Resolver, Server)
            )
        );
    }

    sealed class Resolver(IFormatterResolver standardResolver, Server server) : IFormatterResolver,
        IResolver<TimeZoneOffset>,
        IResolver<DateComponents>,
        IResolver<TemporaryFile>
    {
        readonly TimeZoneOffsetFormatter timeZoneOffsetFormatter = new(standardResolver);
        readonly DateFormatter dateFormatter = new(standardResolver);
        readonly TemporaryFileFormatter temporaryFileFormatter = new(standardResolver, server);

        public IMessagePackFormatter<T>? GetFormatter<T>()
        {
            return (this as IResolver<T>)?.GetFormatter();
        }

        IMessagePackFormatter<TimeZoneOffset>? IResolver<TimeZoneOffset>.GetFormatter()
        {
            return timeZoneOffsetFormatter;
        }

        IMessagePackFormatter<DateComponents>? IResolver<DateComponents>.GetFormatter()
        {
            return dateFormatter;
        }

        IMessagePackFormatter<TemporaryFile>? IResolver<TemporaryFile>.GetFormatter()
        {
            return temporaryFileFormatter;
        }
    }

    interface IResolver<T>
    {
        IMessagePackFormatter<T>? GetFormatter();
    }
}
