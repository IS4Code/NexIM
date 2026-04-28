using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace NexIM.Tools;

partial struct NonEmptyDictionary<TKey, TValue>
{
    public Builder ToBuilder()
    {
        return new(first, _rest?.ToBuilder());
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Builder : IReadOnlyDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>
    {
        public static readonly Builder Empty = default;

        bool firstTaken;
        KeyValuePair<TKey, TValue> first;
        ImmutableSortedDictionary<TKey, TValue>.Builder? rest;

        public readonly int Count => (firstTaken ? 1 : 0) + (rest?.Count ?? 0);

        readonly bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        internal Builder(KeyValuePair<TKey, TValue> first, ImmutableSortedDictionary<TKey, TValue>.Builder? rest)
        {
            this.first = first;
            this.rest = rest;
            firstTaken = true;
        }

        public readonly NonEmptyDictionary<TKey, TValue>? TryToDictionary()
        {
            if(!firstTaken)
            {
                return null;
            }
            return new(first, rest?.ToImmutable() ?? emptyDict);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            if(!firstTaken)
            {
                first = item;
                firstTaken = true;
                return;
            }

            switch(keyComparer.Compare(first.Key, item.Key))
            {
                case 0:
                    // Same as first
                    if(!valueComparer.Equals(first.Value, item.Value))
                    {
                        // Different value
                        throw KeyAlreadyExists(item);
                    }
                    break;
                case < 0:
                    // After first
                    (rest ??= emptyDict.ToBuilder()).Add(item);
                    break;
                default:
                    // Replace first
                    (rest ??= emptyDict.ToBuilder()).Add(first);
                    first = item;
                    break;
            }
        }

        public void Add(TKey key, TValue value)
        {
            Add(new KeyValuePair<TKey, TValue>(key, value));
        }

        public void Add(NonEmptyDictionary<TKey, TValue> items)
        {
            if(!firstTaken)
            {
                this = items.ToBuilder();
                return;
            }

            switch(keyComparer.Compare(first.Key, items.first.Key))
            {
                case 0:
                    // Same first
                    if(!valueComparer.Equals(first.Value, items.first.Value))
                    {
                        // Different value
                        throw KeyAlreadyExists(items.first);
                    }
                    if(rest != null)
                    {
                        rest.AddRange(items.rest);
                    }
                    else
                    {
                        rest = items.rest.ToBuilder();
                    }
                    break;
                case < 0:
                    // Added into rest
                    (rest ??= emptyDict.ToBuilder()).Add(items.first);
                    rest.AddRange(items.rest);
                    break;
                default:
                    // Replaces first
                    (rest ??= emptyDict.ToBuilder()).Add(first);
                    first = items.first;
                    rest.AddRange(items.rest);
                    break;
            };
        }

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            foreach(var item in items)
            {
                Add(item);
            }
        }

        public TValue this[TKey key] {
            get {
                if(!firstTaken)
                {
                    // Not present
                    return emptyDict[key];
                }

                switch(keyComparer.Compare(first.Key, key))
                {
                    case 0:
                        // First
                        return first.Value;
                    default:
                        // Check in rest even if not there
                        if(rest == null)
                        {
                            // Not present
                            return emptyDict[key];
                        }
                        return rest[key];
                }
            }
            set {
                if(!firstTaken)
                {
                    first = new(key, value);
                    firstTaken = true;
                    return;
                }

                switch(keyComparer.Compare(first.Key, key))
                {
                    case 0:
                        // Same as first
                        first = new(key, value);
                        break;
                    case < 0:
                        // After first
                        (rest ??= emptyDict.ToBuilder())[key] = value;
                        break;
                    default:
                        // Replace first
                        (rest ??= emptyDict.ToBuilder())[first.Key] = first.Value;
                        first = new(key, value);
                        break;
                }
            }
        }

#pragma warning disable CS8767
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
#pragma warning restore CS8767
        {
            if(!firstTaken)
            {
                // Not present
                value = default;
                return false;
            }

            switch(keyComparer.Compare(first.Key, key))
            {
                case 0:
                    value = first.Value;
                    return true;
                case < 0:
                    if(rest == null)
                    {
                        // Not present
                        value = default;
                        return false;
                    }
                    return rest.TryGetValue(key, out value);
                default:
                    value = default;
                    return false;
            }
        }

        public bool Remove(TKey key)
        {
            if(!firstTaken)
            {
                return false;
            }

            switch(keyComparer.Compare(first.Key, key))
            {
                case 0:
                    // Same as first
                    if(rest?.Count > 0)
                    {
                        // Select a new first
                        foreach(var pair in rest)
                        {
                            first = pair;
                            break;
                        }
                        rest.Remove(first);
                    }
                    else
                    {
                        firstTaken = false;
                    }
                    return true;
                case < 0:
                    // Remove in rest
                    return (rest?.Remove(key)).GetValueOrDefault();
                default:
                    // Not present
                    return false;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if(!firstTaken)
            {
                return false;
            }

            switch(keyComparer.Compare(first.Key, item.Key))
            {
                case 0:
                    // Same as first
                    if(!valueComparer.Equals(first.Value, item.Value))
                    {
                        // Different value
                        return false;
                    }
                    if(rest?.Count > 0)
                    {
                        // Select a new first
                        foreach(var pair in rest)
                        {
                            first = pair;
                            break;
                        }
                        rest.Remove(first);
                    }
                    else
                    {
                        firstTaken = false;
                    }
                    return true;
                case < 0:
                    // Remove in rest
                    return (rest?.Remove(first)).GetValueOrDefault();
                default:
                    // Not present
                    return false;
            }
        }

        public void Clear()
        {
            firstTaken = false;
            rest?.Clear();
        }

        public IEnumerable<TKey> Keys {
            get {
                if(!firstTaken)
                {
                    yield break;
                }

                yield return first.Key;

                if(rest != null)
                {
                    foreach(var key in rest.Keys)
                    {
                        yield return key;
                    }
                }
            }
        }

        public IEnumerable<TValue> Values {
            get {
                if(!firstTaken)
                {
                    yield break;
                }

                yield return first.Value;

                if(rest != null)
                {
                    foreach(var value in rest.Values)
                    {
                        yield return value;
                    }
                }
            }
        }

        public readonly bool ContainsKey(TKey key)
        {
            if(!firstTaken)
            {
                return false;
            }

            switch(keyComparer.Compare(first.Key, key))
            {
                case 0:
                    // First
                    return true;
                case < 0:
                    // Check in rest
                    return rest?.ContainsKey(key) ?? false;
                default:
                    // Not present
                    return false;
            }
        }

        public readonly bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if(!firstTaken)
            {
                return false;
            }

            switch(keyComparer.Compare(first.Key, item.Key))
            {
                case 0:
                    // Check first
                    return valueComparer.Equals(first.Value, item.Value);
                case < 0:
                    // Check in rest
                    return rest?.Contains(item) ?? false;
                default:
                    // Not present
                    return false;
            }
        }

        public readonly void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach(var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public readonly IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if(!firstTaken)
            {
                yield break;
            }

            yield return first;

            if(rest != null)
            {
                foreach(var item in rest)
                {
                    yield return item;
                }
            }
        }

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
