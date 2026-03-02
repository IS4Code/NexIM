using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Unicord.Server.Primitives;

public readonly struct LocalizedString : IEquatable<LocalizedString>, IEnumerable<LanguageTaggedString>
{
    static readonly ImmutableSortedDictionary<string, string> empty = ImmutableSortedDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

    readonly ImmutableSortedDictionary<string, string>? _data;
    ImmutableSortedDictionary<string, string> data => _data ?? empty;

    public IReadOnlyDictionary<string, string> Data => data;

    public bool Empty => data.Count == 0;

    private LocalizedString(ImmutableSortedDictionary<string, string> data)
    {
        _data = data;
    }

    public LocalizedString(LanguageTaggedString initial)
    {
        _data = empty.SetItem(initial.LanguageTag, initial.Value);
    }

    public LocalizedString Add(LocalizedString other)
    {
        return new(data.AddRange(other.data));
    }

    public LocalizedString Add(LocalizedString? other)
    {
        return other is { } value ? Add(value) : this;
    }

    public LocalizedString Add(LanguageTaggedString other, string? defaultLanguage)
    {
        var languageTag = other.LanguageTag;
        return new(data.Add(String.IsNullOrEmpty(languageTag) ? (defaultLanguage ?? "") : languageTag, other.Value));
    }

    public LocalizedString Add(LanguageTaggedString? other, string? defaultLanguage)
    {
        return other is { } value ? Add(value, defaultLanguage) : this;
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

    sealed class EqualityComparer : IEqualityComparer<KeyValuePair<string, string>>
    {
        public static readonly EqualityComparer Instance = new();

        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return x.Key.Equals(y.Key, StringComparison.OrdinalIgnoreCase) && x.Value == y.Value;
        }

        public int GetHashCode(KeyValuePair<string, string> obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.Key, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(obj.Value);
            return hashCode.ToHashCode();
        }
    }
}
