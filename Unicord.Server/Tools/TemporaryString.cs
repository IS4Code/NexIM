using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Server.Tools;

/// <summary>
/// Provides a mutable string implementation whose contents can be determinstically
/// cleared from memory.
/// </summary>
public sealed class TemporaryString : IList<char>, IDisposable
{
    public delegate int SynchronousReader<TArgs>(ArraySegment<char> outputBuffer, TArgs args);
    public delegate ValueTask<int> AsynchronousReader<TArgs>(ArraySegment<char> outputBuffer, TArgs args);

    public delegate void SynchronousSpanWriter<TArgs>(ReadOnlySpan<char> chars, TArgs args);
    public delegate ValueTask AsynchronousArrayWriter<TArgs>(ArraySegment<char> chars, TArgs args);

    readonly ArrayPool<char> pool;
    readonly char key;

    char[] storage;

    public int Length { get; private set; }

    GCHandle handle;

    private Span<char> Data => storage.AsSpan(0, Length);

    int ICollection<char>.Count => Length;

    bool ICollection<char>.IsReadOnly => false;

    public char this[int index] {
        get {
            var value = Data[index];
            return unchecked((char)(value ^ key));
        }
        set {
            Data[index] = unchecked((char)(value ^ key));
        }
    }

    public TemporaryString(int capacity = 1, ArrayPool<char>? arrayPool = null)
    {
        pool = arrayPool ?? ArrayPool<char>.Shared;
        key = (char)Random.Shared.Next(0, Char.MaxValue);

        storage = pool.Rent(capacity);
        handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
    }

    public void Clear()
    {
        Zero(Data);
        Length = 0;
    }

    public void Reserve(int newLength)
    {
        if(newLength <= storage.Length)
        {
            // Enough capacity
            return;
        }

        // Rent a bigger array
        var newStorage = pool.Rent(newLength);
        var newHandle = GCHandle.Alloc(newStorage, GCHandleType.Pinned);

        // Copy to new storage
        var data = Data;
        data.CopyTo(newStorage.AsSpan());

        // Cleanup
        Zero(data);
        handle.Free();
        pool.Return(storage);

        storage = newStorage;
        handle = newHandle;
    }

    public int IndexOf(char item)
    {
        for(int i = 0; i < Length; i++)
        {
            if(this[i] == item)
            {
                return i;
            }
        }
        return -1;
    }

    public void Insert(int index, char item)
    {
        if(index == Length)
        {
            Append(item);
            return;
        }

        Reserve(Length + 1);

        var buffer = storage.AsSpan(0, Length + 1);

        // Create gap
        buffer.Slice(index, Length - index).CopyTo(buffer.Slice(index + 1));

        buffer[index] = unchecked((char)(item ^ key));

        Length++;
    }

    public void RemoveAt(int index)
    {
        var data = Data;

        Zero(data.Slice(index, 1));

        if(index != Length - 1)
        {
            // Fill the gap
            data.Slice(index + 1).CopyTo(data.Slice(index));
            Zero(data.Slice(Length - 1));
        }

        Length--;
    }

    public void Append(char c)
    {
        int length = Length;
        Reserve(length + 1);

        storage[length] = unchecked((char)(c ^ key));

        Length++;
    }

    public void Append(ReadOnlySpan<char> chars)
    {
        int length = Length, count = chars.Length;
        Reserve(length + count);

        int vectorSize = Vector<ushort>.Count;
        var vectorKey = new Vector<ushort>(key);

        var target = MemoryMarshal.Cast<char, ushort>(storage.AsSpan(length, count));
        var source = MemoryMarshal.Cast<char, ushort>(chars);

        int i;
        for(i = 0; i <= count - vectorSize; i += vectorSize)
        {
            var vector = new Vector<ushort>(source.Slice(i, vectorSize));
            (vector ^ vectorKey).CopyTo(target.Slice(i, vectorSize));
        }

        for(; i < count; i++)
        {
            target[i] = unchecked((char)(source[i] ^ key));
        }

        Length += count;
    }

    public void ReadFrom<TArgs>(SynchronousReader<TArgs> reader, TArgs args)
    {
        int read = 0;
        do
        {
            XorTail(read);
            Length += read;
            Reserve(Length + 1);
        }
        while((read = reader(new(storage, Length, storage.Length - Length), args)) > 0);
    }

    public async ValueTask ReadFromAsync<TArgs>(AsynchronousReader<TArgs> reader, TArgs args)
    {
        int read = 0;
        do
        {
            XorTail(read);
            Length += read;
            Reserve(Length + 1);
        }
        while((read = await reader(new(storage, Length, storage.Length - Length), args)) > 0);
    }

    private void XorTail(int count)
    {
        int vectorSize = Vector<ushort>.Count;
        var vectorKey = new Vector<ushort>(key);

        var data = MemoryMarshal.Cast<char, ushort>(storage.AsSpan(Length, count));

        int i;
        for(i = 0; i <= count - vectorSize; i += vectorSize)
        {
            var span = data.Slice(i, vectorSize);
            var vector = new Vector<ushort>(span);
            (vector ^ vectorKey).CopyTo(span);
        }

        for(; i < count; i++)
        {
            data[i] = unchecked((char)(data[i] ^ key));
        }
    }

    public void WriteTo<TArgs>(SynchronousSpanWriter<TArgs> writer, TArgs args)
    {
        int vectorSize = Vector<ushort>.Count;
        var vectorKey = new Vector<ushort>(key);

        var data = MemoryMarshal.Cast<char, ushort>(Data);
        var count = data.Length;

        Span<Vector<ushort>> output = stackalloc Vector<ushort>[1];

        int i;
        try
        {
            for(i = 0; i <= count - vectorSize; i += vectorSize)
            {
                output[0] = new Vector<ushort>(data.Slice(i, vectorSize)) ^ vectorKey;
                writer(MemoryMarshal.Cast<Vector<ushort>, char>(output), args);
            }
        }
        finally
        {
            Zero(output);
        }

        Span<ushort> remaining = stackalloc ushort[count - i];
        if(remaining.Length == 0)
        {
            return;
        }

        try
        {
            for(int j = 0; j < remaining.Length; j++)
            {
                remaining[j] = unchecked((char)(data[i + j] ^ key));
            }

            writer(MemoryMarshal.Cast<ushort, char>(remaining), args);
        }
        finally
        {
            Zero(remaining);
        }
    }

    public async ValueTask WriteToAsync<TArgs>(AsynchronousArrayWriter<TArgs> writer, TArgs args)
    {
        int vectorSize = Vector<ushort>.Count;
        var vectorKey = new Vector<ushort>(key);

        var count = Length;

        var output = pool.Rent(vectorSize);
        var handle = GCHandle.Alloc(output, GCHandleType.Pinned);
        int remaining = vectorSize;
        try
        {
            int i;
            for(i = 0; i <= count - vectorSize; i += vectorSize)
            {
                (new Vector<ushort>(
                    MemoryMarshal.Cast<char, ushort>(Data).Slice(i, vectorSize)
                ) ^ vectorKey).CopyTo(
                    MemoryMarshal.Cast<char, ushort>(output.AsSpan())
                );
                await writer(new(output, 0, vectorSize), args);
            }

            remaining = count - i;

            // Clear unused elements
            Zero(output.AsSpan(remaining));

            if(remaining == 0)
            {
                return;
            }

            for(int j = 0; j < remaining; j++)
            {
                output[j] = unchecked((char)(Data[i + j] ^ key));
            }

            await writer(new(output, 0, remaining), args);
        }
        finally
        {
            Zero(output.AsSpan(0, remaining));
            handle.Free();
            pool.Return(output);
        }
    }

    public Enumerator GetEnumerator()
    {
        return new(this);
    }

    IEnumerator<char> IEnumerable<char>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<char>.Add(char item)
    {
        Append(item);
    }

    bool ICollection<char>.Contains(char item)
    {
        return IndexOf(item) != -1;
    }

    void ICollection<char>.CopyTo(char[] array, int arrayIndex)
    {
        for(int i = 0; i < Length; i++)
        {
            array[arrayIndex + i] = this[i];
        }
    }

    bool ICollection<char>.Remove(char item)
    {
        int index = IndexOf(item);
        if(index == -1)
        {
            return false;
        }
        RemoveAt(index);
        return true;
    }

    private void Dispose(bool disposing)
    {
        var storage = this.storage;
        if(storage == null || Interlocked.CompareExchange(ref this.storage!, null, storage) != storage)
        {
            return;
        }

        Zero(storage.AsSpan(0, Length));
        handle.Free();
        pool.Return(storage);
    }

    private static void Zero<T>(Span<T> data) where T : unmanaged
    {
        var span = MemoryMarshal.Cast<T, byte>(data);
        CryptographicOperations.ZeroMemory(span);
    }

    ~TemporaryString()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public struct Enumerator : IEnumerator<char>
    {
        readonly TemporaryString source;
        int position;

        internal Enumerator(TemporaryString source)
        {
            this.source = source;
            position = -1;
        }

        public readonly char Current => source[position];

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return ++position < source.Length;
        }

        public void Reset()
        {
            position = -1;
        }

        public void Dispose()
        {
            position = Int32.MaxValue;
        }
    }
}
