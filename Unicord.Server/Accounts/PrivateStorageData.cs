using System;
using System.Threading;
using System.Xml.Linq;
using Unicord.Primitives;
using Unicord.Server.Events;

namespace Unicord.Server.Accounts;

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

    internal Guid AccountIdentifier { get; init; }

    internal PrivateStorageData()
    {

    }

    private PrivateStorageData(PrivateData eventData)
    {
        this.eventData = eventData;
    }

    public static PrivateStorageData Create(PrivateData eventData, LanguageCode? language)
    {
        var key = eventData.Key;
        return new(eventData) {
            KeyName = key.LocalName,
            KeyNamespace = key.NamespaceName,
            Language = language,
            Data = eventData.Extensions
        };
    }
}
