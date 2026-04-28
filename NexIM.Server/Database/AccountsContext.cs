using System;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Database;

internal class AccountsContext : DbContext
{
    public Server Server { get; }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Identity> Identities { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<PrivateStorageData> PrivateStorage { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }

    public IQueryable<Account> FullAccounts => Accounts
        .Include(x => x.Identity)
        .Include(x => x.ContactsBuilder)
        .Include(x => x.PrivateStorageBuilder)
        .Include(x => x.UploadedFilesBuilder);

    readonly VCardConverter vcardConverter;
    readonly EventExtensionsConverter eventExtensionsConverter;
    readonly NullableNonEmptySetConverter<string> stringSetConverter;

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
        stringSetConverter = new(msgpackOptions);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
#if DEBUG
        options.EnableSensitiveDataLogging();
        options.LogTo(str => {
            lock(typeof(Console))
            {
                Console.WriteLine(str);
            }
        }, Microsoft.Extensions.Logging.LogLevel.Information);
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
        modelBuilder.Entity<Identity>(e => {
            e.HasKey(x => x.Identifier);

            // Explicitly mapped because they are passed through the constructor
            e.Property(x => x.User);
            e.Property(x => x.Host);
        });

        modelBuilder.Entity<Account>(e => {
            e.HasKey(x => x.Identifier);
            e.HasOne(x => x.Identity).WithOne().HasForeignKey<Account>(x => x.Identifier);

            e.Property(x => x.PasswordHash);

            e.Property(x => x.VCard).HasConversion(vcardConverter);

            e.HasMany(x => x.ContactsBuilder).WithOne().HasForeignKey(x => x.OwnerIdentifier);
            e.HasMany(x => x.PrivateStorageBuilder).WithOne().HasForeignKey(x => x.OwnerIdentifier);
            e.HasMany(x => x.UploadedFilesBuilder).WithOne(x => x.Uploader);
        });

        modelBuilder.Entity<Contact>(e => {
            e.HasKey(x => new { x.OwnerIdentifier, x.Identifier });
            e.HasOne(x => x.Identity).WithMany().HasForeignKey(x => x.Identifier);

            e.Property(x => x.Groups).HasConversion(stringSetConverter);
        });

        modelBuilder.Entity<PrivateStorageData>(e => {
            e.HasKey(x => new { x.OwnerIdentifier, x.KeyNamespace, x.KeyName });

            e.Property(x => x.Data).HasConversion(eventExtensionsConverter);
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
        IResolver<TemporaryFile?>,
        IResolver<ValueUri>
    {
        readonly TimeZoneOffsetFormatter timeZoneOffsetFormatter = new(standardResolver);
        readonly DateFormatter dateFormatter = new(standardResolver);
        readonly TemporaryFileFormatter temporaryFileFormatter = new(standardResolver, server);
        readonly ValueUriFormatter valueUriFormatter = new(standardResolver);

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

        IMessagePackFormatter<ValueUri>? IResolver<ValueUri>.GetFormatter()
        {
            return valueUriFormatter;
        }
    }

    interface IResolver<T>
    {
        IMessagePackFormatter<T>? GetFormatter();
    }
}
