using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Unicord.Server.Tools;

/// <summary>
/// Provides a mutable array implementation whose contents can be determinstically
/// cleared from memory.
/// </summary>
public class TemporaryArray<T> : IList<T>, IDisposable where T : unmanaged, IEquatable<T>
{
    public delegate int SynchronousReader<TArgs>(ArraySegment<T> outputBuffer, TArgs args);
    public delegate ValueTask<int> AsynchronousReader<TArgs>(ArraySegment<T> outputBuffer, TArgs args);

    readonly ArrayPool<T> pool;

    readonly object syncRoot = new();

    T[] storage;

    public int Length { get; private set; }

    GCHandle handle;

    Span<T> Data => storage.AsSpan(0, Length);

    public ArraySegment<T> Value => new(storage, 0, Length);

    int ICollection<T>.Count => Length;
    bool ICollection<T>.IsReadOnly => false;

    public T this[int index] {
        get {
            return Data[index];
        }
        set {
            Data[index] = value;
        }
    }

    public TemporaryArray(int capacity = 1, ArrayPool<T>? arrayPool = null)
    {
        pool = arrayPool ?? ArrayPool<T>.Shared;

        storage = pool.Rent(capacity);
        handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
    }

    protected TemporaryArray(TemporaryArray<T> original)
    {
        lock(original.syncRoot)
        {
            pool = original.pool;
            Length = original.Length;
            storage = original.storage;
            handle = original.handle;

            original.Length = 0;
            original.storage = pool.Rent(1);
            original.handle = GCHandle.Alloc(original.storage, GCHandleType.Pinned);
        }
    }

    public static TemporaryArray<T> MoveFrom(TemporaryArray<T> original)
    {
        return new(original);
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

        lock(syncRoot)
        {
            // Rent a bigger array
            var newStorage = pool.Rent(newLength);
            var newHandle = GCHandle.Alloc(newStorage, GCHandleType.Pinned);
            try
            {
                // Copy to new storage
                var data = Data;
                data.CopyTo(newStorage.AsSpan());

                // Cleanup
                Zero(data);
                handle.Free();
                if(storage.Length > 0)
                {
                    pool.Return(storage);
                }

                storage = newStorage;
                handle = newHandle;
            }
            catch when(Cleanup())
            {
                // Fatal error, clear all data
            }

            bool Cleanup()
            {
                Zero(newStorage.AsSpan(0, Length));
                newHandle.Free();
                pool.Return(newStorage);
                return false;
            }
        }
    }

    public int IndexOf(T item)
    {
        return Data.IndexOf(item);
    }

    public void Insert(int index, T value)
    {
        if(index == Length)
        {
            Append(value);
            return;
        }

        int newLength = Length + 1;
        Reserve(newLength);
        var buffer = storage.AsSpan(0, newLength);

        // Create gap
        buffer.Slice(index, Length - index).CopyTo(buffer.Slice(index + 1));

        buffer[index] = value;

        Length = newLength;
    }

    public void Insert(int index, ReadOnlySpan<T> items)
    {
        if(index == Length)
        {
            Append(items);
            return;
        }

        int newLength = Length + items.Length;
        Reserve(newLength);
        var buffer = storage.AsSpan(0, newLength);

        // Create gap
        buffer.Slice(index, Length - index).CopyTo(buffer.Slice(index + items.Length));

        items.CopyTo(buffer.Slice(index, items.Length));

        Length = newLength;
    }

    public void RemoveAt(int index)
    {
        var data = Data;

        if(index != Length - 1)
        {
            // Fill the gap
            data.Slice(index + 1).CopyTo(data.Slice(index));
        }
        Zero(data.Slice(Length - 1));

        Length--;
    }

    public void Append(T item)
    {
        int newLength = Length + 1;
        Reserve(newLength);

        storage[Length] = item;

        Length = newLength;
    }

    public void Append(ReadOnlySpan<T> items)
    {
        int newLength = Length + items.Length;
        Reserve(newLength);

        items.CopyTo(storage.AsSpan(Length, items.Length));

        Length = newLength;
    }

    public void ReadFrom<TArgs>(SynchronousReader<TArgs> reader, TArgs args)
    {
        int read = 0;
        do
        {
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
            Length += read;
            Reserve(Length + 1);
        }
        while((read = await reader(new(storage, Length, storage.Length - Length), args)) > 0);
    }

    public ArraySegment<T>.Enumerator GetEnumerator()
    {
        return Value.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<T>.Add(T item)
    {
        Append(item);
    }

    bool ICollection<T>.Contains(T item)
    {
        return IndexOf(item) != -1;
    }

    void ICollection<T>.CopyTo(T[] array, int arrayIndex)
    {
        Data.CopyTo(array.AsSpan(arrayIndex));
    }

    bool ICollection<T>.Remove(T item)
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
        if(storage.Length == 0)
        {
            return;
        }
        lock(syncRoot)
        {
            var storage = this.storage;
            if(storage.Length == 0)
            {
                return;
            }
            int length = Length;

            Length = 0;
            this.storage = Array.Empty<T>();

            Zero(storage.AsSpan(0, length));
            handle.Free();
            pool.Return(storage);
        }
    }

    private static void Zero<TSource>(Span<TSource> data) where TSource : unmanaged
    {
        var span = MemoryMarshal.Cast<TSource, byte>(data);
        CryptographicOperations.ZeroMemory(span);
    }

    ~TemporaryArray()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Provides a mutable string implementation whose contents can be determinstically
/// cleared from memory.
/// </summary>
public class TemporaryString : TemporaryArray<char>
{
    public TemporaryString(int capacity = 1, ArrayPool<char>? arrayPool = null) : base(capacity, arrayPool)
    {

    }

    protected TemporaryString(TemporaryArray<char> original) : base(original)
    {

    }

    public static new TemporaryString MoveFrom(TemporaryArray<char> original)
    {
        return new(original);
    }
}
