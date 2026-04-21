using System;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Unicord.Primitives;
using Unicord.Server.Accounts;

namespace Unicord.Server.Database;

internal class AccountsContext : DbContext
{
    public Server Server { get; }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }

    readonly VCardConverter vcardConverter;
    readonly EventExtensionsConverter eventExtensionsConverter;

    static AccountsContext()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public AccountsContext(Server server)
    {
        Server = server;
        var msgpackOptions = CreateOptions(MessagePackSerializerOptions.Standard);
        vcardConverter = new(msgpackOptions);
        eventExtensionsConverter = new(msgpackOptions);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
#if DEBUG
        options.EnableSensitiveDataLogging();
#endif
        options.UseSqlite("Data Source=accounts.db");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Guid>().HaveConversion<GuidToBytesConverter>();
        configurationBuilder.Properties<LanguageCode?>().HaveConversion<LanguageCodeConverter>();
        configurationBuilder.Properties<SubscriptionState>().HaveConversion<SubscriptionStateConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e => {
            e.HasKey(x => x.Identifier);
            e.HasIndex(x => new { x.Host, x.User });

            e.Property(x => x.PasswordHash);

            e.Property(x => x.VCard).HasConversion(vcardConverter);

            e.OwnsMany(x => x.ContactsBuilder, e => {
                e.WithOwner().HasForeignKey(x => x.AccountIdentifier);
                e.HasKey(x => new { x.AccountIdentifier, x.Host, x.User });
            });

            e.OwnsMany(x => x.PrivateStorageBuilder, e => {
                e.WithOwner().HasForeignKey(x => x.AccountIdentifier);
                e.HasKey(x => new { x.AccountIdentifier, x.KeyNamespace, x.KeyName });

                e.Property(x => x.Data).HasConversion(eventExtensionsConverter);
            });

            e.HasMany(x => x.UploadedFilesBuilder).WithOne(x => x.Uploader);
        });

        modelBuilder.Entity<UploadedFile>(e => {
            e.HasKey(x => x.Identifier);

            e.Property(x => x.Sha1Hash);
            e.Property(x => x.Sha256Hash);
        });
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
        IResolver<TemporaryFile?>
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

        IMessagePackFormatter<TemporaryFile?>? IResolver<TemporaryFile?>.GetFormatter()
        {
            return temporaryFileFormatter;
        }
    }

    interface IResolver<T>
    {
        IMessagePackFormatter<T>? GetFormatter();
    }
}
