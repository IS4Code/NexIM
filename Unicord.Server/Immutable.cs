using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Unicord.Server;

internal static class Immutable
{
    public static TValue AddOrUpdate<TKey, TValue, TArg>(ref ImmutableDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TArg, TValue> addFactory, Func<TKey, TValue, TArg, TValue> updateFactory, out ImmutableDictionary<TKey, TValue> finalState, TArg arg) where TKey : notnull
    {
        while(true)
        {
            // Get the current state
            var original = Volatile.Read(ref dictionary);

            // Create the item
            var item = original.TryGetValue(key, out var existing) ? updateFactory(key, existing, arg) : addFactory(key, arg);

            // Set the item
            var updated = original.SetItem(key, item);
            if(original == updated)
            {
                // No change
                finalState = original;
                return item;
            }

            if(Interlocked.CompareExchange(ref dictionary, updated, original) == original)
            {
                // Unchanged in the meantime, store
                finalState = updated;
                return item;
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
