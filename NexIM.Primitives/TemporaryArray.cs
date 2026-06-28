using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NexIM.Primitives;

/// <summary>
/// Provides a mutable array implementation whose contents can be determinstically
/// cleared from memory.
/// </summary>
public class TemporaryArray<T> : IList<T>, IReadOnlyList<T>, IDisposable where T : unmanaged, IEquatable<T>
{
    public delegate int SynchronousReader<TArgs>(ArraySegment<T> outputBuffer, TArgs args);
    public delegate ValueTask<int> AsynchronousReader<TArgs>(ArraySegment<T> outputBuffer, TArgs args);

    public delegate void SynchronousWriter<TArgs>(ArraySegment<T> inputBuffer, TArgs args);
    public delegate ValueTask AsynchronousWriter<TArgs>(ArraySegment<T> inputBuffer, TArgs args);

    readonly ITemporaryArraySource<T> source;

    readonly object syncRoot = new();

    T[] storage;

    public int Length { get; private set; }

    GCHandle handle;

    Span<T> Data => storage.AsSpan(0, Length);

    public ArraySegment<T> Value => new(storage, 0, Length);

    int ICollection<T>.Count => Length;
    int IReadOnlyCollection<T>.Count => Length;
    bool ICollection<T>.IsReadOnly => false;

    public T this[int index] {
        get => Data[index];
        set => Data[index] = value;
    }

    private protected const int DefaultCapacity = 1;

    public TemporaryArray(int capacity = DefaultCapacity, ITemporaryArraySource<T>? arraySource = null)
    {
        source = arraySource ?? TemporaryArraySource<T>.Shared;

        storage = source.RentArray(capacity);
        handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
    }

    private protected TemporaryArray(TemporaryArray<T> original)
    {
        lock(original.syncRoot)
        {
            source = original.source;
            Length = original.Length;
            storage = original.storage;
            handle = original.handle;

            original.Length = 0;
            original.storage = source.RentArray(1);
            original.handle = GCHandle.Alloc(original.storage, GCHandleType.Pinned);
        }
    }

    [return: NotNullIfNotNull(nameof(original))]
    public static TemporaryArray<T>? MoveFrom(TemporaryArray<T>? original)
    {
        return original is null ? null : new(original);
    }

    public void Clear()
    {
        source.ZeroMemory(Data);
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
            var newStorage = source.RentArray(newLength);
            var newHandle = GCHandle.Alloc(newStorage, GCHandleType.Pinned);
            try
            {
                // Copy to new storage
                var data = Data;
                data.CopyTo(newStorage.AsSpan());

                // Cleanup
                source.ZeroMemory(data);
                handle.Free();
                if(storage.Length > 0)
                {
                    source.ReturnArray(storage);
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
                source.ZeroMemory(newStorage.AsSpan(0, Length));
                newHandle.Free();
                source.ReturnArray(newStorage);
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
        source.ZeroMemory(data.Slice(Length - 1));

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

    public static TemporaryArray<T> CreateFrom<TArgs>(SynchronousReader<TArgs> reader, TArgs args, int capacity = DefaultCapacity, ITemporaryArraySource<T>? arraySource = null)
    {
        var arr = new TemporaryArray<T>(capacity: capacity, arraySource: arraySource);
        try
        {
            arr.ReadFrom(reader, args);
            return arr;
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            throw;
        }

        bool Dispose()
        {
            arr.Dispose();
            return false;
        }
    }

    public static async ValueTask<TemporaryArray<T>> CreateFromAsync<TArgs>(AsynchronousReader<TArgs> reader, TArgs args, int capacity = DefaultCapacity, ITemporaryArraySource<T>? arraySource = null)
    {
        var arr = new TemporaryArray<T>(capacity: capacity, arraySource: arraySource);
        try
        {
            await arr.ReadFromAsync(reader, args);
            return arr;
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            throw;
        }

        bool Dispose()
        {
            arr.Dispose();
            return false;
        }
    }

    public void WriteTo<TArgs>(SynchronousWriter<TArgs> writer, TArgs args)
    {
        writer(Value, args);
    }

    public ValueTask WriteToAsync<TArgs>(AsynchronousWriter<TArgs> writer, TArgs args)
    {
        return writer(Value, args);
    }

    public Span<T>.Enumerator GetEnumerator()
    {
        return Data.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator(Value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator(Value);
    }

    private static IEnumerator<T> GetEnumerator<TSource>(TSource collection) where TSource : struct, IEnumerable<T>
    {
        return collection.GetEnumerator();
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

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Pattern")]
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

            source.ZeroMemory(storage.AsSpan(0, length));
            handle.Free();
            source.ReturnArray(storage);
        }
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
