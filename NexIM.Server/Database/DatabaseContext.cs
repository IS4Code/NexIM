using System;
using System.Net.Mail;
using System.Threading;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Database;

internal abstract class DatabaseContext : DbContext
{
    public NexServer Server { get; }

    public SemaphoreSlim Lock { get; } = new(1, 1);

    public DbSet<Identity> Identities => Set<Identity>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<PrivateStorageData> PrivateStorage => Set<PrivateStorageData>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

    readonly VCardConverter vcardConverter;
    readonly EventExtensionsConverter eventExtensionsConverter;
    readonly NullableNonEmptySetConverter<string> stringSetConverter;

    static DatabaseContext()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public DatabaseContext(NexServer server)
    {
        Server = server;
        var msgpackOptions = CreateOptions(MessagePackSerializerOptions.Standard);
        vcardConverter = new(msgpackOptions);
        eventExtensionsConverter = new(msgpackOptions);
        stringSetConverter = new(msgpackOptions);
    }

    protected sealed override void OnConfiguring(DbContextOptionsBuilder options)
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
        options.UseSqlite(Server.SQLiteConnectionString);
    }

    protected sealed override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Guid>().HaveConversion<GuidToBytesConverter>();
        configurationBuilder.Properties<ArraySegment<byte>>().HaveConversion<ArraySegmentToBytesConverter>();
        configurationBuilder.Properties<LanguageCode?>().HaveConversion<LanguageCodeConverter>();
        configurationBuilder.Properties<SubscriptionState>().HaveConversion<SubscriptionStateConverter>();
        configurationBuilder.Properties<MailAddress>().HaveConversion<MailAddressConverter>();
        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Identity>(e => {
            e.HasKey(x => x.Identifier);

            // Explicitly mapped because they are passed through the constructor
            e.Property(x => x.User);
            e.Property(x => x.Host);

            // Reflects the UUID version, which must be unique together with the account name
            // (a non-owned identity may coexist with owned one when someone registers it)
            e.Property(x => x.Owned);
            e.HasIndex(x => new { x.Owned, x.User, x.Host }).IsUnique();
        });

        modelBuilder.Entity<Account>(e => {
            e.HasKey(x => x.Identifier);
            e.HasOne(x => x.Identity).WithOne().HasForeignKey<Account>(x => x.Identifier);

            e.Property(x => x.PasswordHash);

            e.Property(x => x.VCard).HasConversion(vcardConverter!);

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
                // Must go first to avoid unwrapping the element type
                new Resolver(from.Resolver, Server),
                from.Resolver
            )
        );
    }

    sealed class Resolver(IFormatterResolver standardResolver, NexServer server) : IFormatterResolver,
        IResolver<TimeZoneOffset>,
        IResolver<DateComponents>,
        IResolver<Remote<TemporaryFile>?>,
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

        IMessagePackFormatter<Remote<TemporaryFile>?>? IResolver<Remote<TemporaryFile>?>.GetFormatter()
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
