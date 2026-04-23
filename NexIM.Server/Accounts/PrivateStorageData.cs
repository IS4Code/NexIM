using System;
using System.Threading;
using System.Xml.Linq;
using NexIM.Primitives;
using NexIM.Server.Events;

namespace NexIM.Server.Accounts;

public record PrivateStorageData
{
    public required string KeyName { get; init; }
    public required string KeyNamespace { get; init; }
    public required LanguageCode? Language { get; init; }
    public required EventExtensions Data { get; init; }

    PrivateData? eventData;

    public PrivateData EventData => LazyInitializer.EnsureInitialized(ref eventData, () => new() {
        Key = XName.Get(KeyName, KeyNamespace),
        Extensions = Data
    });

    internal Guid OwnerIdentifier { get; }

    private PrivateStorageData(Guid ownerIdentifier)
    {
        // DB constructor
        OwnerIdentifier = ownerIdentifier;
    }

    internal PrivateStorageData(Account owner) : this(owner.Identifier)
    {

    }

    private PrivateStorageData(Account owner, PrivateData eventData)
    {
        OwnerIdentifier = owner.Identifier;
        this.eventData = eventData;
    }

    internal static PrivateStorageData Create(Account owner, PrivateData eventData, LanguageCode? language)
    {
        var key = eventData.Key;
        return new(owner, eventData) {
            KeyName = key.LocalName,
            KeyNamespace = key.NamespaceName,
            Language = language,
            Data = eventData.Extensions
        };
    }
}
