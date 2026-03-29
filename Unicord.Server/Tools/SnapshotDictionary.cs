using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace Unicord.Server.Tools;

[StructLayout(LayoutKind.Auto)]
internal struct SnapshotDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IEquatable<SnapshotDictionary<TKey, TValue>> where TKey : notnull
{
    static readonly ImmutableDictionary<TKey, TValue> defaultStorage = ImmutableDictionary<TKey, TValue>.Empty;

    ImmutableDictionary<TKey, TValue>? _storage;
    readonly ImmutableDictionary<TKey, TValue> storage => _storage ?? defaultStorage;

    public readonly IDictionary<TKey, TValue> Snapshot => storage;

    public readonly Enumerator GetEnumerator()
    {
        return new(storage.GetEnumerator());
    }

    public readonly bool Equals(SnapshotDictionary<TKey, TValue> other)
    {
        return storage.Equals(other.storage);
    }

    public readonly override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is SnapshotDictionary<TKey, TValue> other && Equals(other);
    }

    public readonly override int GetHashCode()
    {
        return storage.GetHashCode();
    }

    internal static class Storage
    {
        public static ImmutableDictionary<TKey, TValue> Get(ref SnapshotDictionary<TKey, TValue> dict)
        {
            return Volatile.Read(ref dict._storage) ?? defaultStorage;
        }

        public static bool TryUpdate(ref SnapshotDictionary<TKey, TValue> dict, ImmutableDictionary<TKey, TValue> updated, ImmutableDictionary<TKey, TValue> original)
        {
            ref var storage = ref dict._storage;

            // Swap out null for default
            Interlocked.CompareExchange(ref storage, defaultStorage, null);

            // Try updating
            return original == Interlocked.CompareExchange(ref storage, updated, original);
        }
    }

    #region IReadOnlyDictionary implementation
    public readonly TValue this[TKey key] => storage[key];
    public readonly IEnumerable<TKey> Keys => storage.Keys;
    public readonly IEnumerable<TValue> Values => storage.Values;
    public readonly int Count => storage.Count;

    public readonly bool ContainsKey(TKey key) => storage.ContainsKey(key);
    public readonly bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => storage.TryGetValue(key, out value);

    readonly IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<TKey, TValue>>)storage).GetEnumerator();
    }

    readonly IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)storage).GetEnumerator();
    }
    #endregion

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        ImmutableDictionary<TKey, TValue>.Enumerator enumerator;

        internal Enumerator(ImmutableDictionary<TKey, TValue>.Enumerator enumerator)
        {
            this.enumerator = enumerator;
        }

        public KeyValuePair<TKey, TValue> Current => enumerator.Current;
        object IEnumerator.Current => Current;
        public bool MoveNext() => enumerator.MoveNext();
        public void Reset() => enumerator.Reset();
        public void Dispose() => enumerator.Dispose();
    }
}
