using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using NexIM.Primitives.Xml;

namespace NexIM.Tools;

/// <summary>
/// Provides a thread-safe implementation of <see cref="XmlMemoryNameTable"/>.
/// </summary>
public class XmlStaticNameTable : XmlMemoryNameTable
{
    readonly ConcurrentDictionary<string, ValueTuple> data;
    readonly ConcurrentDictionary<string, ValueTuple>.AlternateLookup<ReadOnlyMemory<char>> lookup;

    public XmlStaticNameTable()
    {
        data = new(Comparer.Instance);
        lookup = data.GetAlternateLookup<ReadOnlyMemory<char>>();
    }

    public override string Add(ReadOnlyMemory<char> memory)
    {
        lookup.TryAdd(memory, default);
        // Either just added or already present
        return Get(memory) ?? throw new NotSupportedException();
    }

    public override string? Get(ReadOnlyMemory<char> memory)
    {
        return lookup.TryGetValue(memory, out var key, out _) ? key : null;
    }

    sealed class Comparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlyMemory<char>, string>
    {
        static readonly StringComparer comparer = StringComparer.Ordinal;
        static readonly CompareInfo compareInfo = CultureInfo.InvariantCulture.CompareInfo;

        public static readonly Comparer Instance = new();

        private Comparer()
        {

        }

        public bool Equals(string? x, string? y)
        {
            return comparer.Equals(x, y);
        }

        public int GetHashCode(string obj)
        {
            return GetHashCode(obj.AsMemory());
        }

        public bool Equals(ReadOnlyMemory<char> alternate, string other)
        {
            return alternate.Span.Equals(other.AsSpan(), StringComparison.Ordinal);
        }

        public int GetHashCode(ReadOnlyMemory<char> alternate)
        {
            return compareInfo.GetHashCode(alternate.Span, CompareOptions.Ordinal);
        }

        public string Create(ReadOnlyMemory<char> alternate)
        {
            return alternate.ToString();
        }
    }
}
