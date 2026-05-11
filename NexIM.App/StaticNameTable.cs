using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NexIM.App.Configuration.Grammar;

namespace NexIM.App;

sealed class StaticNameTable : Vocabulary
{
    readonly HashSet<string> table = new(Comparer.Instance);
    HashSet<string>.AlternateLookup<ReadOnlyMemory<char>> alternate => table.GetAlternateLookup<string, ReadOnlyMemory<char>>();

    public override string Add(ReadOnlyMemory<char> memory)
    {
        if(alternate.TryGetValue(memory, out var result))
        {
            return result;
        }
        var str = Comparer.Instance.Create(memory);
        table.Add(str);
        return str;
    }

    public override string? Get(ReadOnlyMemory<char> memory)
    {
        return alternate.TryGetValue(memory, out var result) ? result : null;
    }

    sealed class Comparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlyMemory<char>, string>
    {
        static readonly StringComparer comparer = StringComparer.Ordinal;

        public static readonly Comparer Instance = new();

        private Comparer()
        {

        }

        public bool Equals(string? x, string? y)
        {
            return comparer.Equals(x, y);
        }

        public int GetHashCode([DisallowNull] string obj)
        {
            return GetHashCode(obj.AsMemory());
        }

        public bool Equals(ReadOnlyMemory<char> alternate, string other)
        {
            return alternate.Span.Equals(other.AsSpan(), StringComparison.Ordinal);
        }

        public int GetHashCode(ReadOnlyMemory<char> alternate)
        {
            var hashCode = new HashCode();
            hashCode.AddBytes(MemoryMarshal.Cast<char, byte>(alternate.Span));
            return hashCode.ToHashCode();
        }

        public string Create(ReadOnlyMemory<char> alternate)
        {
            return alternate.ToString();
        }
    }
}
