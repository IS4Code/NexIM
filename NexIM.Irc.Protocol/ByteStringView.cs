using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using NexIM.Primitives.Tools;

namespace NexIM.Irc.Protocol;

public readonly struct ByteStringView(ReadOnlyMemory<byte> data) : IEquatable<ByteStringView>, IComparable<ByteStringView>, IList<byte>, IReadOnlyList<byte>
{
    public static readonly ByteStringView Empty = default;

    ReadOnlySpan<byte> span => data.Span;

    public int Length => data.Length;
    public bool IsEmpty => data.IsEmpty;

    int ICollection<byte>.Count => Length;
    int IReadOnlyCollection<byte>.Count => Length;
    bool ICollection<byte>.IsReadOnly => true;

    byte IList<byte>.this[int index] { get => span[index]; set => throw new NotSupportedException(); }
    byte IReadOnlyList<byte>.this[int index] => span[index];

    public ByteStringView(ReadOnlyMemory<byte> buffer, ReadOnlySpan<byte> rangeSpan) : this(SliceToSpan(buffer, rangeSpan))
    {

    }

    static ReadOnlyMemory<byte> SliceToSpan(ReadOnlyMemory<byte> data, ReadOnlySpan<byte> rangeSpan)
    {
        if(rangeSpan.IsEmpty)
        {
            return default;
        }
        if(!data.Span.Overlaps(rangeSpan, out var start) || start + rangeSpan.Length > data.Length)
        {
            throw new ArgumentException("The span is not contained", nameof(rangeSpan));
        }
        return data.Slice(start, rangeSpan.Length);
    }

    public ReadOnlySpan<byte> AsSpan() => span;
    public ReadOnlyMemory<byte> AsMemory() => data;
    public int CompareTo(ByteStringView other) => span.SequenceCompareTo(other.span);
    public bool Equals(ByteStringView other) => span.SequenceEqual(other.span);
    public int IndexOf(byte value) => span.IndexOf(value);
    public int IndexOfAny(ReadOnlySpan<byte> values) => span.IndexOfAny(values);
    public void CopyTo(Span<byte> span) => this.span.CopyTo(span);
    public void CopyTo(Span<byte> span, int index) => this.span.CopyTo(span.Slice(index));
    public ByteStringView Slice(int start) => new(data.Slice(start));
    public ByteStringView Slice(int start, int length) => new(data.Slice(start, length));

    public ByteStringView Preserve() => span.ToArray();

    [Obsolete("Using this method always results in array allocation."), EditorBrowsable(EditorBrowsableState.Never)]
    public byte[] ToArray() => data.ToArray();

    public override bool Equals(object obj)
    {
        return obj is ByteStringView other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(span);
        return hashCode.ToHashCode();
    }

    bool ICollection<byte>.Contains(byte item) => span.IndexOf(item) != -1;
    void ICollection<byte>.CopyTo(byte[] array, int arrayIndex) => CopyTo(array.AsSpan(arrayIndex));

    public override string ToString()
    {
        return ToString(Encoding.UTF8);
    }

    public unsafe string ToString(Encoding encoding)
    {
        var span = this.span;
        if(span.IsEmpty)
        {
            return "";
        }
        fixed(byte* start = span)
        {
            return encoding.GetString(start, span.Length);
        }
    }

    IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
    {
        if(MemoryMarshal.TryGetArray(data, out var segment))
        {
            for(int i = 0; i < Length; i++)
            {
                yield return segment.Array[segment.Offset + i];
            }
        }
        else
        {
            for(int i = 0; i < Length; i++)
            {
                yield return span[i];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator(this);
        static IEnumerator<byte> GetEnumerator<TEnumerable>(TEnumerable enumerable) where TEnumerable : IEnumerable<byte>
        {
            return enumerable.GetEnumerator();
        }
    }

    void ICollection<byte>.Add(byte item) => throw new NotSupportedException();
    void ICollection<byte>.Clear() => throw new NotSupportedException();
    bool ICollection<byte>.Remove(byte item) => throw new NotSupportedException();
    void IList<byte>.Insert(int index, byte item) => throw new NotSupportedException();
    void IList<byte>.RemoveAt(int index) => throw new NotSupportedException();

    public static bool operator ==(ByteStringView a, ByteStringView b) => a.Equals(b);
    public static bool operator !=(ByteStringView a, ByteStringView b) => !a.Equals(b);

    public static implicit operator ByteStringView(byte[] data) => new(data);
    public static implicit operator ByteStringView(ArraySegment<byte> data) => new(data);
    public static implicit operator ByteStringView(Memory<byte> data) => new(data);
    public static implicit operator ByteStringView(ReadOnlyMemory<byte> data) => new(data);
    public static implicit operator ReadOnlySpan<byte>(ByteStringView str) => str.AsSpan();
    public static implicit operator ReadOnlyMemory<byte>(ByteStringView str) => str.AsMemory();
}
