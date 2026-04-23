using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace NexIM.Server.Tools;

[StructLayout(LayoutKind.Auto)]
internal partial struct SnapshotDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IEquatable<SnapshotDictionary<TKey, TValue>> where TKey : notnull
{
    static readonly ImmutableDictionary<TKey, TValue> defaultStorage = ImmutableDictionary<TKey, TValue>.Empty;

    ImmutableDictionary<TKey, TValue>? _storage;
    readonly ImmutableDictionary<TKey, TValue> storage => _storage ?? defaultStorage;

    public readonly IReadOnlyCollection<TKey> Keys => new KeysCollection(storage);
    public readonly IReadOnlyCollection<TValue> Values => new ValuesCollection(storage);

    public SnapshotDictionary(IDictionary<TKey, TValue> entries)
    {
        _storage = entries switch {
            ImmutableDictionary<TKey, TValue> existing => existing,
            ImmutableDictionary<TKey, TValue>.Builder builder => builder.ToImmutable(),
            _ => ImmutableDictionary.CreateRange(entries)
        };
    }

    internal SnapshotDictionary(ImmutableDictionary<TKey, TValue> entries)
    {
        _storage = entries;
    }

    private SnapshotDictionary(ImmutableDictionary<TKey, TValue>.Builder builder)
    {
        _storage = builder.ToImmutable();
    }

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

    #region Interface implementation
    public readonly int Count => storage.Count;
    public readonly TValue this[TKey key] => storage[key];

    readonly IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => storage.Keys;
    readonly IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => storage.Values;
    readonly ICollection<TKey> IDictionary<TKey, TValue>.Keys => new KeysCollection(storage);
    readonly ICollection<TValue> IDictionary<TKey, TValue>.Values => new ValuesCollection(storage);

    readonly bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;

    readonly TValue IDictionary<TKey, TValue>.this[TKey key] {
        get => this[key];
        set => throw new NotSupportedException();
    }

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

    public readonly bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return storage.Contains(item);
    }

    readonly void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        foreach(var pair in storage)
        {
            array[arrayIndex++] = pair;
        }
    }

    readonly void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new NotSupportedException();
    readonly void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
    readonly bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new NotSupportedException();
    readonly bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
    readonly void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new NotSupportedException();
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
