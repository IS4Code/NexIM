using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace NexIM.Xmpp.Tools;

internal class EncodedStringComparer(Encoding encoding) : IEqualityComparer<string>, IComparer<string>, IEqualityComparer<ReadOnlyMemory<char>>, IComparer<ReadOnlyMemory<char>>, IAlternateEqualityComparer<ReadOnlyMemory<char>, string>, IAlternateEqualityComparer<ReadOnlyMemory<byte>, string>, IAlternateEqualityComparer<ReadOnlyMemory<byte>, ReadOnlyMemory<char>>
{
    static readonly ArrayPool<byte> pool = ArrayPool<byte>.Shared;

    public int Compare(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
    {
        var xb = Encode(x, out var xa);
        try
        {
            var yb = Encode(y, out var ya);
            try
            {
                return xb.Span.SequenceCompareTo(yb.Span);
            }
            finally
            {
                pool.Return(ya);
            }
        }
        finally
        {
            pool.Return(xa);
        }
    }

    public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
    {
        if(encoding is UTF8Encoding or UnicodeEncoding or UTF32Encoding)
        {
            // Supports all characters
            return x.Span.Equals(y.Span, StringComparison.Ordinal);
        }
        var xb = Encode(x, out var xa);
        try
        {
            var yb = Encode(y, out var ya);
            try
            {
                return xb.Span.SequenceEqual(yb.Span);
            }
            finally
            {
                pool.Return(ya);
            }
        }
        finally
        {
            pool.Return(xa);
        }
    }

    public int GetHashCode(ReadOnlyMemory<char> obj)
    {
        var encoded = Encode(obj, out var array);
        try
        {
            return EncodedHashCode(encoded);
        }
        finally
        {
            pool.Return(array);
        }
    }

    private ReadOnlyMemory<byte> Encode(ReadOnlyMemory<char> input, out byte[] array)
    {
        var count = encoding.GetByteCount(input.Span);
        array = pool.Rent(count);
        if(!encoding.TryGetBytes(input.Span, array.AsSpan(), out count))
        {
            throw new NotSupportedException();
        }
        return array.AsMemory(0, count);
    }

    private bool EncodedEquals(ReadOnlyMemory<byte> alternate, ReadOnlyMemory<char> other)
    {
        var encoded = Encode(other, out var array);
        try
        {
            return alternate.Span.SequenceEqual(encoded.Span);
        }
        finally
        {
            pool.Return(array);
        }
    }

    private int EncodedHashCode(ReadOnlyMemory<byte> data)
    {
        var code = new HashCode();
        code.AddBytes(data.Span);
        return code.ToHashCode();
    }

    private string EncodedCreate(ReadOnlyMemory<byte> data)
    {
        return encoding.GetString(data.Span);
    }

    public int Compare(string? x, string? y)
    {
        if(x is null)
        {
            if(y is null)
            {
                return 0;
            }
            return -1;
        }
        else if(y is null)
        {
            return 1;
        }
        return Compare(x.AsMemory(), y.AsMemory());
    }

    public bool Equals(string? x, string? y)
    {
        if(x is null || y is null)
        {
            return Object.ReferenceEquals(x, y);
        }
        return Equals(x.AsMemory(), y.AsMemory());
    }

    public int GetHashCode(string obj)
    {
        return GetHashCode(obj.AsMemory());
    }

    string IAlternateEqualityComparer<ReadOnlyMemory<char>, string>.Create(ReadOnlyMemory<char> alternate)
    {
        return alternate.ToString();
    }

    bool IAlternateEqualityComparer<ReadOnlyMemory<char>, string>.Equals(ReadOnlyMemory<char> alternate, string other)
    {
        if(other is null)
        {
            return false;
        }
        return Equals(alternate, other.AsMemory());
    }

    bool IAlternateEqualityComparer<ReadOnlyMemory<byte>, string>.Equals(ReadOnlyMemory<byte> alternate, string other)
    {
        if(other is null)
        {
            return false;
        }
        return EncodedEquals(alternate, other.AsMemory());
    }

    bool IAlternateEqualityComparer<ReadOnlyMemory<byte>, ReadOnlyMemory<char>>.Equals(ReadOnlyMemory<byte> alternate, ReadOnlyMemory<char> other)
    {
        return EncodedEquals(alternate, other);
    }

    string IAlternateEqualityComparer<ReadOnlyMemory<byte>, string>.Create(ReadOnlyMemory<byte> alternate)
    {
        return EncodedCreate(alternate);
    }

    ReadOnlyMemory<char> IAlternateEqualityComparer<ReadOnlyMemory<byte>, ReadOnlyMemory<char>>.Create(ReadOnlyMemory<byte> alternate)
    {
        return EncodedCreate(alternate).AsMemory();
    }

    int IAlternateEqualityComparer<ReadOnlyMemory<byte>, string>.GetHashCode(ReadOnlyMemory<byte> alternate)
    {
        return EncodedHashCode(alternate);
    }

    int IAlternateEqualityComparer<ReadOnlyMemory<byte>, ReadOnlyMemory<char>>.GetHashCode(ReadOnlyMemory<byte> alternate)
    {
        return EncodedHashCode(alternate);
    }
}

internal sealed class Utf8StringComparer : EncodedStringComparer
{
    public static readonly Utf8StringComparer Instance = new();

    private Utf8StringComparer() : base(new UTF8Encoding(false))
    {

    }
}
