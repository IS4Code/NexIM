using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NexIM.Server.Tools;

internal static class SnapshotDictionaryExtensions
{
    static class Storage<TKey, TValue> where TKey : notnull where TValue : class
    {
        public static readonly Func<TKey, TValue, TValue?> SimpleAddFactory = (key, arg) => arg;
        public static readonly Func<TKey, TValue, TValue, TValue?> SimpleUpdateFactory = (key, previous, arg) => arg;
    }

    public static void SetItem<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull where TValue : class
    {
        SetItem(ref dictionary, key, value, out _, out _, out _);
    }

    public static void SetItem<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key, TValue value, out TValue? previous, out TValue? updated, out SnapshotDictionary<TKey, TValue> snapshot) where TKey : notnull where TValue : class
    {
        AddOrUpdate(ref dictionary, key, Storage<TKey, TValue>.SimpleAddFactory, Storage<TKey, TValue>.SimpleUpdateFactory, out previous, out updated, out snapshot, value);
    }

    public static void Clear<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        Clear(ref dictionary, out _);
    }

    public static void Clear<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, out SnapshotDictionary<TKey, TValue> snapshot) where TKey : notnull
    {
        while(true)
        {
            // Get the current state
            var originalDict = SnapshotDictionary<TKey, TValue>.Storage.Get(ref dictionary);

            // Update the dictionary
            var updatedDict = originalDict.Clear();
            if(originalDict == updatedDict)
            {
                // No change
                snapshot = new(originalDict);
                return;
            }

            if(SnapshotDictionary<TKey, TValue>.Storage.TryUpdate(ref dictionary, updatedDict, originalDict))
            {
                // Unchanged in the meantime, store
                snapshot = new(updatedDict);
                return;
            }
        }
    }

    public static bool AddOrUpdate<TKey, TValue, TArg>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TArg, TValue?> addFactory, Func<TKey, TValue, TArg, TValue?> updateFactory, out TValue? previous, out TValue? updated, out SnapshotDictionary<TKey, TValue> snapshot, TArg arg) where TKey : notnull where TValue : class
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
                    snapshot = new(originalDict);
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
                snapshot = new(originalDict);
                return false;
            }

            if(SnapshotDictionary<TKey, TValue>.Storage.TryUpdate(ref dictionary, updatedDict, originalDict))
            {
                // Unchanged in the meantime, store
                updated = item;
                snapshot = new(updatedDict);
                return true;
            }
        }
    }

    public static bool TryRemove<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull
    {
        return TryRemove(ref dictionary, key, out _, out _);
    }

    public static bool TryRemove<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, TKey key, [MaybeNullWhen(false)] out TValue value, out SnapshotDictionary<TKey, TValue> snapshot) where TKey : notnull
    {
        while(true)
        {
            // Get the current state
            var original = SnapshotDictionary<TKey, TValue>.Storage.Get(ref dictionary);

            if(!original.TryGetValue(key, out value))
            {
                // Not present
                snapshot = new(original);
                return false;
            }

            // Remove the item
            var updated = original.Remove(key);

            if(SnapshotDictionary<TKey, TValue>.Storage.TryUpdate(ref dictionary, updated, original))
            {
                // Unchanged in the meantime, store
                snapshot = new(updated);
                return true;
            }
        }
    }

    public static bool TryRemove<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, KeyValuePair<TKey, TValue> pair) where TKey : notnull where TValue : class
    {
        return TryRemove(ref dictionary, pair, out _);
    }

    public static bool TryRemove<TKey, TValue>(ref this SnapshotDictionary<TKey, TValue> dictionary, KeyValuePair<TKey, TValue> pair, out SnapshotDictionary<TKey, TValue> snapshot) where TKey : notnull where TValue : class
    {
        while(true)
        {
            // Get the current state
            var original = SnapshotDictionary<TKey, TValue>.Storage.Get(ref dictionary);

            if(!original.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                // Not present or has wrong value
                snapshot = new(original);
                return false;
            }

            // Remove the item
            var updated = original.Remove(pair.Key);

            if(SnapshotDictionary<TKey, TValue>.Storage.TryUpdate(ref dictionary, updated, original))
            {
                // Unchanged in the meantime, store
                snapshot = new(updated);
                return true;
            }
        }
    }
}
