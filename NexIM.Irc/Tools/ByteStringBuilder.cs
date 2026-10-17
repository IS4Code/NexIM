using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NexIM.Tools;

namespace NexIM.Irc.Tools;

[InterpolatedStringHandler, StructLayout(LayoutKind.Auto)]
#pragma warning disable CS9113
internal readonly struct ByteStringBuilder(int literalLength, int formattedCount, ByteStringBuilder.IProvider provider)
#pragma warning restore CS9113
{
    readonly List<(bstring value, byte[]? rented)> chunks = new(formattedCount * 2 + 1);

    public bool IsEmpty => chunks.Count == 0;

    public void AppendLiteral(string s)
    {
        AppendFormatted(s);
    }

    public void AppendFormatted(string s)
    {
        AppendFormatted(provider.Encoder.Get(s));
    }

    public void AppendFormatted(bstring s)
    {
        chunks.Add((s, null));
    }

    public void AppendFormatted(ReadOnlySpan<byte> s)
    {
        var pool = provider.ArrayPool;
        var array = pool.Rent(s.Length);
        s.CopyTo(array);
        chunks.Add((array.AsMemory(0, s.Length), array));
    }

    public void AppendFormatted<T>(T t) where T : IUtf8SpanFormattable, IFormattable
    {
        AppendFormatted(t, "");
    }

    public void AppendFormatted<T>(T t, string format) where T : IUtf8SpanFormattable, IFormattable
    {
        int size = 16;
        while(size <= 256)
        {
            // Restrict rented size
            var pool = provider.ArrayPool;
            var array = pool.Rent(size);
            if(t.TryFormat(array, out var written, format, provider.FormatProvider))
            {
                chunks.Add((array.AsMemory(0, written), array));
                return;
            }
            size = array.Length * 2;
            pool.Return(array);
        }
        // Fallback
        AppendLiteral(t.ToString(format, provider.FormatProvider));
    }

    public readonly bstring ToByteString(out byte[] rentedBuffer)
    {
        int total = chunks.Sum(c => c.value.Length);
        var pool = provider.ArrayPool;
        rentedBuffer = pool.Rent(total);
        var span = rentedBuffer.AsSpan(0, total);
        foreach(var (chunk, chunkBuffer) in chunks)
        {
            chunk.CopyTo(span);
            span = span.Slice(chunk.Length);
            if(chunkBuffer != null)
            {
                pool.Return(chunkBuffer);
            }
        }
        chunks.Clear();
        return rentedBuffer.AsMemory(0, total);
    }

    public interface IProvider
    {
        StringTransformationCache<bstring> Encoder { get; }
        IFormatProvider FormatProvider { get; }
        ArrayPool<byte> ArrayPool { get; }
    }
}
