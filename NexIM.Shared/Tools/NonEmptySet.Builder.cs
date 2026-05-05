using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace NexIM.Tools;

#if DEFINE_TOOLS

partial struct NonEmptySet<T>
{
    public Builder ToBuilder()
    {
        return new(first, _rest?.ToBuilder());
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Builder : IReadOnlyCollection<T>, ICollection<T>
    {
        public static readonly Builder Empty = default;

        bool firstTaken;
        T first;
        ImmutableSortedSet<T>.Builder? rest;

        public readonly int Count => (firstTaken ? 1 : 0) + (rest?.Count ?? 0);

        readonly bool ICollection<T>.IsReadOnly => false;

        internal Builder(T first, ImmutableSortedSet<T>.Builder? rest)
        {
            this.first = first;
            this.rest = rest;
            firstTaken = true;
        }

        public readonly NonEmptySet<T>? TryToSet()
        {
            if(!firstTaken)
            {
                return null;
            }
            return new(first, rest?.ToImmutable() ?? emptySet);
        }

        public bool Add(T item)
        {
            if(!firstTaken)
            {
                first = item;
                firstTaken = true;
                return true;
            }

            switch(comparer.Compare(first, item))
            {
                case 0:
                    // Same as first
                    return false;
                case < 0:
                    // After first
                    return (rest ??= emptySet.ToBuilder()).Add(item);
                default:
                    // Replace first
                    (rest ??= emptySet.ToBuilder()).Add(first);
                    first = item;
                    return true;
            }
        }

        void ICollection<T>.Add(T item) => Add(item);

        public void Add(NonEmptySet<T> items)
        {
            if(!firstTaken)
            {
                this = items.ToBuilder();
                return;
            }

            switch(comparer.Compare(first, items.first))
            {
                case 0:
                    // Same first
                    if(rest != null)
                    {
                        rest.UnionWith(items.rest);
                    }
                    else
                    {
                        rest = items.rest.ToBuilder();
                    }
                    break;
                case < 0:
                    // Added into rest
                    (rest ??= emptySet.ToBuilder()).Add(items.first);
                    rest.UnionWith(items.rest);
                    break;
                default:
                    // Replaces first
                    (rest ??= emptySet.ToBuilder()).Add(first);
                    first = items.first;
                    rest.UnionWith(items.rest);
                    break;
            };
        }

        public void AddRange(IEnumerable<T> items)
        {
            foreach(var item in items)
            {
                Add(item);
            }
        }

        public bool Remove(T item)
        {
            if(!firstTaken)
            {
                return false;
            }

            switch(comparer.Compare(first, item))
            {
                case 0:
                    // Same as first
                    if(rest?.Count > 0)
                    {
                        // Select a new first
                        first = rest[0];
                        rest.Remove(first);
                    }
                    else
                    {
                        firstTaken = false;
                    }
                    return true;
                case < 0:
                    // Remove in rest
                    return rest?.Remove(item) ?? false;
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

        public readonly bool Contains(T item)
        {
            if(!firstTaken)
            {
                return false;
            }

            switch(comparer.Compare(first, item))
            {
                case 0:
                    // First
                    return true;
                case < 0:
                    // Check in rest
                    return rest?.Contains(item) ?? false;
                default:
                    // Not present
                    return false;
            }
        }

        public readonly void CopyTo(T[] array, int arrayIndex)
        {
            foreach(var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public readonly IEnumerator<T> GetEnumerator()
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

#endif
