using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using NexIM.Tools;

namespace NexIM.Irc.Tools;

internal sealed class ByteStringCache
{
    readonly ConcurrentDictionary<Encoding, Cache> cache = new(EncodingComparer.Instance);
    readonly Func<Encoding, Cache> factory = enc => new Cache(enc);

    public static readonly ByteStringCache Global = new();

    public StringTransformationCache<bstring, IUtf8SpanFormattable> Get(Encoding encoding)
    {
        return cache.GetOrAdd(encoding, factory);
    }

    public bstring Get(Encoding encoding, string input)
    {
        return Get(encoding).Get(input);
    }

    sealed class Cache(Encoding encoding) : StringTransformationCache<bstring, IUtf8SpanFormattable>
    {
        protected override bstring Concat(ReadOnlySpan<bstring> results)
        {
            int length = 0;
            foreach(var result in results)
            {
                length += result.Length;
            }
            var array = new byte[length];
            var span = array.AsSpan();
            foreach(var result in results)
            {
                span.Write(result);
            }
            return array;
        }

        protected override bstring Transform(string input)
        {
            return encoding.GetBytes(input);
        }

        protected override bstring Transform<TValue>(TValue value, string format)
        {
            Span<byte> span = stackalloc byte[256];
            if(value.TryFormat(span, out var written, format, CultureInfo.InvariantCulture))
            {
                return span.Slice(0, written).ToArray();
            }
            return Transform(value.ToString(format, CultureInfo.InvariantCulture));
        }
    }

    sealed class EncodingComparer : IEqualityComparer<Encoding>
    {
        public static readonly EncodingComparer Instance = new();

        private EncodingComparer()
        {

        }

        public bool Equals(Encoding? x, Encoding? y)
        {
            return x?.WebName == y?.WebName;
        }

        public int GetHashCode([DisallowNull] Encoding obj)
        {
            return obj.WebName.GetHashCode();
        }
    }
}
