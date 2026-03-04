using System;
using System.Buffers;

namespace Unicord.Primitives;

public interface ITemporaryArraySource<T> where T : unmanaged
{
    T[] RentArray(int capacity);
    void ReturnArray(T[] array);
    void ZeroMemory(Span<T> span);
}

public class TemporaryArraySource<T> : ITemporaryArraySource<T> where T : unmanaged
{
    readonly ArrayPool<T> pool;

    public static readonly TemporaryArraySource<T> Shared = new(ArrayPool<T>.Shared);

    public TemporaryArraySource(ArrayPool<T> arrayPool)
    {
        pool = arrayPool;
    }

    public virtual T[] RentArray(int capacity)
    {
        return pool.Rent(capacity);
    }

    public virtual void ReturnArray(T[] array)
    {
        pool.Return(array);
    }

    public virtual void ZeroMemory(Span<T> span)
    {
        span.Clear();
    }
}
