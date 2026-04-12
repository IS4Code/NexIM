using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

namespace Unicord.Server.Tools;

/// <summary>
/// Stores an ordered set of <typeparamref name="T"/> that has at least 1 element.
/// </summary>
/// <typeparam name="T">The type of the elements in the set.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct NonEmptySet<T> : IReadOnlyCollection<T>, IEquatable<NonEmptySet<T>> where T : IComparable<T>
{
    static readonly Comparer<T> comparer = Comparer<T>.Default;

    static readonly ImmutableSortedSet<T> emptySet = ImmutableSortedSet.Create<T>(comparer);

    readonly T first;
    readonly ImmutableSortedSet<T>? _rest;
    ImmutableSortedSet<T> rest {
        get => _rest ?? emptySet;
        init => _rest = value;
    }

    public int Count => rest.Count + 1;

    private NonEmptySet(T first, ImmutableSortedSet<T> rest)
    {
        this.first = first;
        this.rest = rest;
    }

    private NonEmptySet(T first, ImmutableSortedSet<T>.Builder builder)
    {
        if(builder.Count == 0)
        {
            this.first = first;
            return;
        }
        var testFirst = builder[0];
        switch(comparer.Compare(first, testFirst))
        {
            case 0: // Same as first
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
    }

    public NonEmptySet(T element)
    {
        first = element;
    }

    public bool Contains(T value)
    {
        return comparer.Compare(first, value) == 0 || rest.Contains(value);
    }

    public bool TryGetSingle(out T value)
    {
        value = first;
        return rest.IsEmpty;
    }

    public NonEmptySet<T> Add(T value)
    {
        return comparer.Compare(first, value) switch
        {
            0 => this, // Same first
            < 0 => new(first, rest.Add(value)), // Added into rest
            _ => new(value, rest.Add(first)), // Replaces first
        };
    }

    public NonEmptySet<T> Add(NonEmptySet<T> values)
    {
        return comparer.Compare(first, values.first) switch
        {
            0 => new(first, rest.Union(values.rest)), // Same first
            < 0 => new(first, rest.Union(values.rest).Add(values.first)), // Added into rest
            _ => new(values.first, rest.Union(values.rest).Add(first)) // Replaces first
        };
    }

    public NonEmptySet<T> AddRange(IEnumerable<T> values)
    {
        var builder = rest.ToBuilder();
        builder.UnionWith(values);
        if(builder.Count <= rest.Count)
        {
            // No difference
            return this;
        }
        return new(first, builder);
    }

    public bool TryRemove(T value, out NonEmptySet<T> result)
    {
        if(comparer.Compare(first, value) != 0)
        {
            // Not removing first
            result = rest.IsEmpty ? this : new(first, rest.Remove(value));
            return true;
        }
        return TryCreateFrom(rest, out result);
    }

    public bool TryRemove(NonEmptySet<T> values, out NonEmptySet<T> result)
    {
        if(!values.Contains(first))
        {
            // Not removing first
            result = rest.IsEmpty ? this : new(first, rest.Except(values));
            return true;
        }
        var builder = rest.ToBuilder();
        builder.ExceptWith(values.rest);
        builder.Remove(values.first);
        return TryCreateFrom(builder, out result);
    }

    public bool TryRemoveRange(IEnumerable<T> values, out NonEmptySet<T> result)
    {
        var builder = rest.ToBuilder();
        builder.Add(first);
        builder.ExceptWith(values);
        return TryCreateFrom(builder, out result);
    }

    private bool TryCreateFrom(ImmutableSortedSet<T> set, out NonEmptySet<T> result)
    {
        if(set.IsEmpty)
        {
            // Must not be empty
            result = this;
            return false;
        }
        var newFirst = set[0];
        result = new(newFirst, set.Remove(newFirst));
        return true;
    }

    private bool TryCreateFrom(ImmutableSortedSet<T>.Builder builder, out NonEmptySet<T> result)
    {
        if(builder.Count == 0)
        {
            // Must not be empty
            result = this;
            return false;
        }
        var newFirst = builder[0];
        builder.Remove(newFirst);
        result = new(newFirst, builder.ToImmutable());
        return true;
    }

    public static bool TryCreateRange(IEnumerable<T> range, out NonEmptySet<T> result)
    {
        var builder = emptySet.ToBuilder();
        builder.UnionWith(range);
        return default(NonEmptySet<T>).TryCreateFrom(builder, out result);
    }

    public Partitioner<TKey> OrderedPartitionBy<TKey>(Func<T, TKey> keyFactory)
    {
        return new(this, keyFactory);
    }

    public static implicit operator NonEmptySet<T>(T element)
    {
        return new(element);
    }

    public bool Equals(NonEmptySet<T> other)
    {
        return comparer.Compare(first, other.first) == 0 && rest.SetEquals(other.rest);
    }

    public override bool Equals(object? obj)
    {
        return obj is NonEmptySet<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(first);
        foreach(var value in rest)
        {
            hashCode.Add(value);
        }
        return hashCode.ToHashCode();
    }

    public static bool operator ==(NonEmptySet<T> a, NonEmptySet<T> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(NonEmptySet<T> a, NonEmptySet<T> b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return String.Join(", ", rest.Prepend(first));
    }

    public Enumerator GetEnumerator()
    {
        return new(first, rest.GetEnumerator());
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<T>
    {
        readonly T first;
        State state;
        ImmutableSortedSet<T>.Enumerator inner;

        internal Enumerator(T first, ImmutableSortedSet<T>.Enumerator inner)
        {
            this.first = first;
            this.inner = inner;
        }

        public readonly T Current => state == State.OnFirst ? first : inner.Current;

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

    [StructLayout(LayoutKind.Auto)]
    public readonly struct Partitioner<TKey> : IEnumerable<KeyValuePair<TKey, NonEmptySet<T>>>
    {
        static readonly EqualityComparer<TKey> keyComparer = EqualityComparer<TKey>.Default;

        readonly NonEmptySet<T> source;
        readonly Func<T, TKey> keyFactory;

        internal Partitioner(NonEmptySet<T> source, Func<T, TKey> keyFactory)
        {
            this.source = source;
            this.keyFactory = keyFactory;
        }

        public Enumerator GetEnumerator()
        {
            return new(source.GetEnumerator(), keyFactory);
        }

        IEnumerator<KeyValuePair<TKey, NonEmptySet<T>>> IEnumerable<KeyValuePair<TKey, NonEmptySet<T>>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, NonEmptySet<T>>>
        {
            NonEmptySet<T>.Enumerator enumerator;
            readonly Func<T, TKey> keyFactory;

            State state;
            T? first;
            readonly ImmutableSortedSet<T>.Builder builder;
            TKey? key, nextKey;

            public readonly KeyValuePair<TKey, NonEmptySet<T>> Current => state != State.Initial ? new(key!, new(first!, builder.ToImmutable())) : throw new InvalidOperationException();

            readonly object IEnumerator.Current => Current;

            internal Enumerator(NonEmptySet<T>.Enumerator enumerator, Func<T, TKey> keyFactory)
            {
                this.enumerator = enumerator;
                this.keyFactory = keyFactory;
                builder = emptySet.ToBuilder();
            }

            public bool MoveNext()
            {
                switch(state)
                {
                    case State.Initial:
                    {
                        if(!enumerator.MoveNext())
                        {
                            // Already ended for some reason
                            return false;
                        }
                        // First element
                        state = State.Result;
                        first = enumerator.Current;
                        key = keyFactory(first);
                        break;
                    }
                    case State.Result:
                        // Invoked after previous result
                        key = nextKey;
                        first = enumerator.Current;
                        builder.Clear();
                        break;
                    case State.Finished:
                        return false;
                }

                while(enumerator.MoveNext())
                {
                    var next = enumerator.Current;

                    var newKey = keyFactory(next);
                    if(!keyComparer.Equals(key, newKey))
                    {
                        // A new partition - stop
                        nextKey = newKey;
                        return true;
                    }

                    // Add current element
                    builder.Add(next);
                }

                // All partition variables are stored
                state = State.Finished;
                return true;
            }

            public void Reset()
            {
                enumerator.Reset();
                builder.Clear();
                state = State.Initial;
            }

            public void Dispose()
            {
                enumerator.Dispose();
            }

            enum State
            {
                Initial,
                Result,
                Finished
            }
        }
    }
}
