using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NexIM.Primitives;

public readonly struct LocalizedString : IEquatable<LocalizedString>, IEnumerable<LanguageTaggedString>
{
    static readonly ImmutableSortedDictionary<LanguageCode, string> empty = ImmutableSortedDictionary<LanguageCode, string>.Empty;

    // Sorted to allow sequential equality check
    readonly ImmutableSortedDictionary<LanguageCode, string>? _data;
    ImmutableSortedDictionary<LanguageCode, string> data => _data ?? empty;

    public IReadOnlyDictionary<LanguageCode, string> Data => data;

    public bool Empty => data.Count == 0;

    private LocalizedString(ImmutableSortedDictionary<LanguageCode, string> data)
    {
        _data = data;
    }

    public LocalizedString(LanguageTaggedString initial)
    {
        _data = empty.SetItem(initial.Language, initial.Value);
    }

    public LocalizedString Add(LanguageTaggedString other)
    {
        return new(data.Add(other.Language, other.Value));
    }

    public LocalizedString Add(LanguageTaggedString? other)
    {
        return other is { } value ? Add(value) : this;
    }

    public LocalizedString Add(LocalizedString other)
    {
        return new(data.AddRange(other.data));
    }

    public LocalizedString Add(LocalizedString? other)
    {
        return other is { } value ? Add(value) : this;
    }

    public override bool Equals(object obj)
    {
        return obj is LocalizedString other && Equals(other);
    }

    public bool Equals(LocalizedString other)
    {
        return data.Count == other.data.Count && data.SequenceEqual(other.data, EqualityComparer.Instance);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach(var pair in data)
        {
            hashCode.Add(pair, EqualityComparer.Instance);
        }
        return hashCode.ToHashCode();
    }

    public IEnumerator<LanguageTaggedString> GetEnumerator()
    {
        foreach(var pair in data)
        {
            yield return new(pair.Value, pair.Key);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return String.Join(", ", data.Values);
    }

    sealed class EqualityComparer : IEqualityComparer<KeyValuePair<LanguageCode, string>>
    {
        public static readonly EqualityComparer Instance = new();

        public bool Equals(KeyValuePair<LanguageCode, string> x, KeyValuePair<LanguageCode, string> y)
        {
            return new LanguageTaggedString(x.Value, x.Key) == new LanguageTaggedString(y.Value, y.Key);
        }

        public int GetHashCode(KeyValuePair<LanguageCode, string> obj)
        {
            return new LanguageTaggedString(obj.Value, obj.Key).GetHashCode();
        }
    }
}
