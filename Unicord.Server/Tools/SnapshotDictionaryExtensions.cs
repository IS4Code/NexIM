using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Unicord.Server.Tools;

internal static class SnapshotDictionaryExtensions
{
    public static bool AddOrUpdate<TKey, TValue, TArg>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TArg, TValue?> addFactory, Func<TKey, TValue, TArg, TValue?> updateFactory, out TValue? previous, out TValue? updated, out IDictionary<TKey, TValue> snapshot, TArg arg) where TKey : notnull where TValue : class
    {
        while(true)
        {
            // Get the current state
            var originalDict = SnapshotDictionary<TKey, TValue>.Storage.Get(ref dictionary);

            // Create the item
            TValue? item;
            if(originalDict.TryGetValue(key, out var existing))
            {
                previous = existing;
                item = updateFactory(key, existing, arg);
                if(item == existing)
                {
                    // No change
                    updated = existing;
                    snapshot = originalDict;
                    return false;
                }
            }
            else
            {
                previous = null;
                item = addFactory(key, arg);
            }

            // Process the item
            var updatedDict = item == null ? originalDict.Remove(key) : originalDict.SetItem(key, item);
            if(originalDict == updatedDict)
            {
                // No change
                updated = item;
                snapshot = originalDict;
                return false;
            }

            if(SnapshotDictionary<TKey, TValue>.Storage.TryUpdate(ref dictionary, updatedDict, originalDict))
            {
                // Unchanged in the meantime, store
                updated = item;
                snapshot = updatedDict;
                return true;
            }
        }
    }

    public static bool TryRemove<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key, [MaybeNullWhen(false)] out TValue value, out IDictionary<TKey, TValue> snapshot) where TKey : notnull
    {
        while(true)
        {
            // Get the current state
            var original = SnapshotDictionary<TKey, TValue>.Storage.Get(ref dictionary);

            if(!original.TryGetValue(key, out value))
            {
                // Not present
                snapshot = original;
                return false;
            }

            // Remove the item
            var updated = original.Remove(key);

            if(SnapshotDictionary<TKey, TValue>.Storage.TryUpdate(ref dictionary, updated, original))
            {
                // Unchanged in the meantime, store
                snapshot = updated;
                return true;
            }
        }
    }
}
