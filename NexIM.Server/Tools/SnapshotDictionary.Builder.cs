using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace NexIM.Server.Tools;

partial struct SnapshotDictionary<TKey, TValue>
{
    public readonly Builder ToBuilder()
    {
        return new(storage);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct Builder : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        readonly ImmutableDictionary<TKey, TValue>.Builder builder;

        private IDictionary<TKey, TValue> Dictionary => builder;

        internal Builder(ImmutableDictionary<TKey, TValue> dict)
        {
            builder = dict.ToBuilder();
        }

        public SnapshotDictionary<TKey, TValue> ToDictionary() => new(builder);

        public TValue this[TKey key] { get => builder[key]; set => builder[key] = value; }

        public ICollection<TKey> Keys => Dictionary.Keys;

        public ICollection<TValue> Values => Dictionary.Values;

        public int Count => builder.Count;

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => builder.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => builder.Values;

        public void Add(TKey key, TValue value)
        {
            builder.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            builder.Add(item);
        }

        public void Clear()
        {
            builder.Clear();
        }

        public bool Remove(TKey key)
        {
            return builder.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return builder.Remove(item);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return builder.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return builder.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            Dictionary.CopyTo(array, arrayIndex);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return builder.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
