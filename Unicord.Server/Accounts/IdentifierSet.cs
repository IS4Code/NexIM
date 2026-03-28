using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Unicord.Server.Accounts;

[StructLayout(LayoutKind.Auto)]
public readonly struct IdentifierSet : IReadOnlyCollection<Identifier>, IEquatable<IdentifierSet>
{
    static IComparer<Identifier> comparer => Comparer.Instance;

    static readonly ImmutableSortedSet<Identifier> emptySet = ImmutableSortedSet.Create(comparer);

    public static readonly IdentifierSet Empty = default;

    readonly ImmutableSortedSet<Identifier>? _data;
    ImmutableSortedSet<Identifier> data => _data ?? emptySet;

    public int Count => data.Count;
    public bool IsEmpty => Count == 0;

    private IdentifierSet(ImmutableSortedSet<Identifier> data)
    {
        _data = data;
    }

    public IdentifierSet(Identifier identifier)
    {
        _data = ImmutableSortedSet.Create(comparer, identifier);
    }

    public IdentifierSet(Identifier? identifier)
    {
        if(identifier is { } value)
        {
            _data = ImmutableSortedSet.Create(comparer, value);
        }
    }

    public IdentifierSet(IEnumerable<Identifier> identifiers)
    {
        _data = ImmutableSortedSet.CreateRange(comparer, identifiers);
    }

    public bool Contains(Identifier value)
    {
        return data.Contains(value);
    }

    public bool TryGetSingle(out Identifier value)
    {
        if(Count == 1)
        {
            value = data[0];
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public IdentifierSet Add(Identifier value)
    {
        return new(data.Add(value));
    }

    public IdentifierSet Add(IdentifierSet values)
    {
        return AddRange(values.data);
    }

    public IdentifierSet AddRange(IEnumerable<Identifier> values)
    {
        return new(data.Union(values));
    }

    public IdentifierSet Remove(Identifier value)
    {
        return new(data.Remove(value));
    }

    public IdentifierSet Remove(IdentifierSet values)
    {
        return RemoveRange(values.data);
    }

    public IdentifierSet RemoveRange(IEnumerable<Identifier> values)
    {
        return new(data.Except(values));
    }

    public IEnumerable<KeyValuePair<TKey, IdentifierSet>> OrderedPartitionBy<TKey>(Func<Identifier, TKey> keyFactory) where TKey : notnull, IEquatable<TKey>
    {
        switch(Count)
        {
            case 0:
                yield break;
            case 1:
                // Single element has single partition
                yield return new KeyValuePair<TKey, IdentifierSet>(keyFactory(data[0]), this);
                yield break;
        }

        ImmutableSortedSet<Identifier>.Builder? builder = null;
        TKey? previousKey = default;

        foreach(var identifier in data)
        {
            if(builder == null)
            {
                // First pass
                builder = ImmutableSortedSet.CreateBuilder(comparer);
                previousKey = keyFactory(identifier);
            }
            else
            {
                var key = keyFactory(identifier);
                if(!key.Equals(previousKey))
                {
                    // A new partition - output the previous one
                    yield return new(previousKey!, new(builder.ToImmutable()));
                    builder.Clear();
                    previousKey = key;
                }
            }

            // Add current identifier
            builder.Add(identifier);
        }

        if(builder != null)
        {
            // Final partition
            yield return new(previousKey!, new(builder.ToImmutable()));
        }
    }

    public bool Equals(IdentifierSet other)
    {
        return data.SetEquals(other.data);
    }

    public override bool Equals(object? obj)
    {
        return obj is IdentifierSet other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach(var value in data)
        {
            hashCode.Add(value);
        }
        return hashCode.ToHashCode();
    }

    public static bool operator ==(IdentifierSet a, IdentifierSet b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(IdentifierSet a, IdentifierSet b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return String.Join(", ", data);
    }

    public Enumerator GetEnumerator()
    {
        return new(data.GetEnumerator());
    }

    IEnumerator<Identifier> IEnumerable<Identifier>.GetEnumerator()
    {
        return ((IEnumerable<Identifier>)data).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Identifier>
    {
        ImmutableSortedSet<Identifier>.Enumerator inner;

        internal Enumerator(ImmutableSortedSet<Identifier>.Enumerator inner)
        {
            this.inner = inner;
        }

        public Identifier Current => inner.Current;

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return inner.MoveNext();
        }

        public void Reset()
        {
            inner.Reset();
        }

        public void Dispose()
        {
            inner.Dispose();
        }
    }

    sealed class Comparer : IComparer<Identifier>
    {
        public static Comparer Instance = new();

        private Comparer()
        {

        }

        public int Compare(Identifier x, Identifier y)
        {
            // TODO Order by server, account, resource
            return StringComparer.Ordinal.Compare(x.ToString(), y.ToString());
        }
    }
}
