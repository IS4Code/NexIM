using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NexIM.Server.Tools;

partial struct SnapshotDictionary<TKey, TValue>
{
    abstract class CollectionBase
    {
        public bool IsReadOnly => true;

        public void Clear()
        {
            throw new NotSupportedException();
        }
    }

    sealed class KeysCollection(ImmutableDictionary<TKey, TValue> storage) : CollectionBase, ICollection<TKey>, IReadOnlyCollection<TKey>
    {
        public int Count => storage.Count;

        public IEnumerator<TKey> GetEnumerator()
        {
            return storage.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override int GetHashCode()
        {
            return storage.GetHashCode();
        }

        public bool Contains(TKey item)
        {
            return storage.ContainsKey(item);
        }

        public void CopyTo(TKey[] array, int arrayIndex)
        {
            foreach(var key in storage.Keys)
            {
                array[arrayIndex++] = key;
            }
        }

        public void Add(TKey item) => throw new NotSupportedException();
        public bool Remove(TKey item) => throw new NotImplementedException();
    }

    sealed class ValuesCollection(ImmutableDictionary<TKey, TValue> storage) : CollectionBase, ICollection<TValue>, IReadOnlyCollection<TValue>
    {
        public int Count => storage.Count;

        public IEnumerator<TValue> GetEnumerator()
        {
            return storage.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override int GetHashCode()
        {
            return storage.GetHashCode();
        }

        public bool Contains(TValue item)
        {
            return storage.ContainsValue(item);
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            foreach(var value in storage.Values)
            {
                array[arrayIndex++] = value;
            }
        }

        public void Add(TValue item) => throw new NotSupportedException();
        public bool Remove(TValue item) => throw new NotImplementedException();
    }
}
