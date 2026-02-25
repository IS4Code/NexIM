using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Unicord.Server;

internal static class Immutable
{
    public static bool AddOrUpdate<TKey, TValue, TArg>(ref ImmutableDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TArg, TValue?> addFactory, Func<TKey, TValue, TArg, TValue?> updateFactory, out TValue? previous, out TValue? updated, out ImmutableDictionary<TKey, TValue> finalState, TArg arg) where TKey : notnull where TValue : class
    {
        while(true)
        {
            // Get the current state
            var originalDict = Volatile.Read(ref dictionary);

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
                    finalState = originalDict;
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
                finalState = originalDict;
                return false;
            }

            if(Interlocked.CompareExchange(ref dictionary, updatedDict, originalDict) == originalDict)
            {
                // Unchanged in the meantime, store
                updated = item;
                finalState = updatedDict;
                return true;
            }
        }
    }

    public static bool TryRemove<TKey, TValue>(ref ImmutableDictionary<TKey, TValue> dictionary, TKey key, [MaybeNullWhen(false)] out TValue value, out ImmutableDictionary<TKey, TValue> finalState) where TKey : notnull
    {
        while(true)
        {
            // Get the current state
            var original = Volatile.Read(ref dictionary);

            if(!original.TryGetValue(key, out value))
            {
                // Not present
                finalState = original;
                return false;
            }

            // Remove the item
            var updated = original.Remove(key);

            if(Interlocked.CompareExchange(ref dictionary, updated, original) == original)
            {
                // Unchanged in the meantime, store
                finalState = updated;
                return true;
            }
        }
    }
}
