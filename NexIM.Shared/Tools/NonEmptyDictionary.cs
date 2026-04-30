using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace NexIM.Tools;

#if DEFINE_TOOLS

/// <summary>
/// Stores an ordered dictionary of <typeparamref name="TKey"/>-<typeparamref name="TValue"/> pairs that has at least 1 pair.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
[StructLayout(LayoutKind.Auto)]
#if TOOLS_PUBLIC
public
#endif
readonly partial struct NonEmptyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>, ICollection<KeyValuePair<TKey, TValue>>, IEquatable<NonEmptyDictionary<TKey, TValue>> where TKey : IComparable<TKey>
{
    static readonly IComparer<TKey> keyComparer =
        typeof(string).Equals(typeof(TKey))
        ? (IComparer<TKey>)(object)StringComparer.Ordinal
        : Comparer<TKey>.Default;

    static readonly IEqualityComparer<TValue> valueComparer =
        typeof(string).Equals(typeof(TValue))
        ? (IEqualityComparer<TValue>)(object)StringComparer.Ordinal
        : EqualityComparer<TValue>.Default;

    static readonly ImmutableSortedDictionary<TKey, TValue> emptyDict = ImmutableSortedDictionary.Create(keyComparer, valueComparer);

    readonly KeyValuePair<TKey, TValue> first;
    readonly ImmutableSortedDictionary<TKey, TValue>? _rest;
    ImmutableSortedDictionary<TKey, TValue> rest {
        get => _rest ?? emptyDict;
        init => _rest = value == emptyDict ? null : value;
    }

    public int Count => rest.Count + 1;

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;

    private NonEmptyDictionary(KeyValuePair<TKey, TValue> first, ImmutableSortedDictionary<TKey, TValue> rest)
    {
        this.first = first;
        this.rest = rest;
    }

    private NonEmptyDictionary(KeyValuePair<TKey, TValue> first, ImmutableSortedDictionary<TKey, TValue>.Builder builder)
    {
        if(builder.Count == 0)
        {
            this.first = first;
            return;
        }
        foreach(var testFirst in builder)
        {
            switch(keyComparer.Compare(first.Key, testFirst.Key))
            {
                case 0: // Same as first
                    if(!valueComparer.Equals(first.Value, testFirst.Value))
                    {
                        // Different value
                        throw KeyAlreadyExists(first);
                    }
                    builder.Remove(testFirst);
                    break;
                case < 0: // After first
                    break;
                default: // Replaces first
                    builder.Add(first);
                    first = testFirst;
                    break;
            }
            this.first = first;
            rest = builder.ToImmutable();
            return;
        }
    }

    private static Exception KeyAlreadyExists(KeyValuePair<TKey, TValue> item)
    {
        return new ArgumentException("Key already exists in the dictionary.", nameof(item));
    }

    public NonEmptyDictionary(KeyValuePair<TKey, TValue> item)
    {
        first = item;
    }

    public IEnumerable<TKey> Keys {
        get {
            yield return first.Key;
            foreach(var key in rest.Keys)
            {
                yield return key;
            }
        }
    }

    public IEnumerable<TValue> Values {
        get {
            yield return first.Value;
            foreach(var value in rest.Values)
            {
                yield return value;
            }
        }
    }

    public bool ContainsKey(TKey key)
    {
        return keyComparer.Compare(first.Key, key) switch {
            0 => true, // First
            < 0 => rest.ContainsKey(key), // Check in rest
            _ => false, // Not present
        };
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return keyComparer.Compare(first.Key, item.Key) switch {
            0 => valueComparer.Equals(first.Value, item.Value), // First
            < 0 => rest.Contains(item), // Check in rest
            _ => false, // Not present
        };
    }

    public TValue this[TKey key] {
        get {
            return keyComparer.Compare(first.Key, key) switch {
                0 => first.Value, // First
                _ => rest[key] // Check in rest even if not there
            };
        }
    }

#pragma warning disable CS8767
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
#pragma warning restore CS8767
    {
        switch(keyComparer.Compare(first.Key, key))
        {
            case 0:
                value = first.Value;
                return true;
            case < 0:
                return rest.TryGetValue(key, out value);
            default:
                value = default;
                return false;
        }
    }

    public bool TryGetSingle(out KeyValuePair<TKey, TValue> item)
    {
        item = first;
        return rest.IsEmpty;
    }

    public NonEmptyDictionary<TKey, TValue> Add(KeyValuePair<TKey, TValue> item)
    {
        return keyComparer.Compare(first.Key, item.Key) switch {
            0 => valueComparer.Equals(first.Value, item.Value)
                ? this // Same first
                :  throw KeyAlreadyExists(item),
            < 0 => new(first, rest.Add(item.Key, item.Value)), // Added into rest
            _ => new(item, rest.Add(first.Key, first.Value)), // Replaces first
        };
    }

    public NonEmptyDictionary<TKey, TValue> Add(TKey key, TValue value)
    {
        return Add(new KeyValuePair<TKey, TValue>(key, value));
    }

    public NonEmptyDictionary<TKey, TValue> Add(NonEmptyDictionary<TKey, TValue> items)
    {
        return keyComparer.Compare(first.Key, items.first.Key) switch {
            0 => valueComparer.Equals(first.Value, items.first.Value)
                ? new(first, rest.AddRange(items.rest)) // Same first
                : throw KeyAlreadyExists(items.first),
            < 0 => new(first, rest.AddRange(items.rest).Add(items.first.Key, items.first.Value)), // Added into rest
            _ => new(items.first, rest.AddRange(items.rest).Add(first.Key, first.Value)) // Replaces first
        };
    }

    public NonEmptyDictionary<TKey, TValue> AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        var builder = rest.ToBuilder();
        builder.AddRange(items);
        if(builder.Count <= rest.Count)
        {
            // No difference
            return this;
        }
        return new(first, builder);
    }

    public NonEmptyDictionary<TKey, TValue> SetItem(KeyValuePair<TKey, TValue> item)
    {
        return keyComparer.Compare(first.Key, item.Key) switch {
            0 => new(item, rest), // Sets first
            < 0 => new(first, rest.SetItem(item.Key, item.Value)), // Added into rest
            _ => new(item, rest.SetItem(first.Key, first.Value)), // Replaces first
        };
    }

    public NonEmptyDictionary<TKey, TValue> SetItem(TKey key, TValue value)
    {
        return SetItem(new KeyValuePair<TKey, TValue>(key, value));
    }

    public bool TryRemove(TKey key, out NonEmptyDictionary<TKey, TValue> result)
    {
        if(keyComparer.Compare(first.Key, key) != 0)
        {
            // Not removing first
            result = rest.IsEmpty ? this : new(first, rest.Remove(key));
            return true;
        }
        return TryCreateFrom(rest, out result);
    }

    public bool TryRemoveRange(IEnumerable<TKey> keys, out NonEmptyDictionary<TKey, TValue> result)
    {
        var builder = rest.ToBuilder();
        builder.Add(first);
        builder.RemoveRange(keys);
        return TryCreateFrom(builder, out result);
    }

    private bool TryCreateFrom(ImmutableSortedDictionary<TKey, TValue> dict, out NonEmptyDictionary<TKey, TValue> result)
    {
        if(dict.IsEmpty)
        {
            // Must not be empty
            result = this;
            return false;
        }
        foreach(var newFirst in dict)
        {
            result = new(newFirst, dict.Remove(newFirst.Key));
            return true;
        }
        // Should not happen
        result = this;
        return false;
    }

    private bool TryCreateFrom(ImmutableSortedDictionary<TKey, TValue>.Builder builder, out NonEmptyDictionary<TKey, TValue> result)
    {
        if(builder.Count == 0)
        {
            // Must not be empty
            result = this;
            return false;
        }
        foreach(var newFirst in builder)
        {
            builder.Remove(newFirst);
            result = new(newFirst, builder.ToImmutable());
            return true;
        }
        // Should not happen
        result = this;
        return false;
    }

    public static bool TryCreateRange(IEnumerable<KeyValuePair<TKey, TValue>> items, out NonEmptyDictionary<TKey, TValue> result)
    {
        var builder = emptyDict.ToBuilder();
        builder.AddRange(items);
        return default(NonEmptyDictionary<TKey, TValue>).TryCreateFrom(builder, out result);
    }

    public static implicit operator NonEmptyDictionary<TKey, TValue>(KeyValuePair<TKey, TValue> item)
    {
        return new(item);
    }

    public bool Equals(NonEmptyDictionary<TKey, TValue> other)
    {
        if(rest.Count != other.Count)
        {
            return false;
        }
        if(!PairEquals(first, other.first))
        {
            return false;
        }
        if(rest == other.rest)
        {
            // Same reference
            return true;
        }
        // Compare by items
        using var e = GetEnumerator();
        foreach(var item in other)
        {
            if(!e.MoveNext())
            {
                return false;
            }
            var current = e.Current;
            if(!PairEquals(current, item))
            {
                return false;
            }
        }
        return !e.MoveNext();
    }

    static bool PairEquals(KeyValuePair<TKey, TValue> first, KeyValuePair<TKey, TValue> second)
    {
        return keyComparer.Compare(first.Key, second.Key) == 0 && valueComparer.Equals(first.Value, second.Value);
    }

    public override bool Equals(object? obj)
    {
        return obj is NonEmptyDictionary<TKey, TValue> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(first);
        foreach(var item in rest)
        {
            hashCode.Add(item);
        }
        return hashCode.ToHashCode();
    }

    public static bool operator ==(NonEmptyDictionary<TKey, TValue> a, NonEmptyDictionary<TKey, TValue> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(NonEmptyDictionary<TKey, TValue> a, NonEmptyDictionary<TKey, TValue> b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return String.Join(", ", rest.Prepend(first));
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        foreach(var item in this)
        {
            array[arrayIndex++] = item;
        }
    }

    public Enumerator GetEnumerator()
    {
        return new(first, rest.GetEnumerator());
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
    {
        throw new NotSupportedException();
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Clear()
    {
        throw new NotSupportedException();
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        throw new NotSupportedException();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        readonly KeyValuePair<TKey, TValue> first;
        State state;
        ImmutableSortedDictionary<TKey, TValue>.Enumerator inner;

        internal Enumerator(KeyValuePair<TKey, TValue> first, ImmutableSortedDictionary<TKey, TValue>.Enumerator inner)
        {
            this.first = first;
            this.inner = inner;
        }

        public readonly KeyValuePair<TKey, TValue> Current => state == State.OnFirst ? first : inner.Current;

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            switch(state)
            {
                case State.Initial:
                    state = State.OnFirst;
                    return true;
                case State.OnFirst:
                    state = State.OnRest;
                    break;
            }
            return inner.MoveNext();
        }

        public void Reset()
        {
            inner.Reset();
            state = State.Initial;
        }

        public void Dispose()
        {
            inner.Dispose();
            state = State.OnRest;
        }

        enum State
        {
            Initial,
            OnFirst,
            OnRest
        }
    }
}

#endif
