using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

public interface ITypedEncoder<T>
{
    ValueTask<T> Decode(XmlReader reader);
    ValueTask Encode(T value, XmlWriter writer);
}

public static class TypedEncoder<T>
{
    public static ValueTask Encode<TEncoder>(TEncoder encoder, T value, XmlWriter writer) where TEncoder : ITypedEncoder<T>
    {
        return encoder.Encode(value, writer);
    }

    public static ValueTask<T> Decode<TEncoder>(TEncoder encoder, XmlReader reader) where TEncoder : ITypedEncoder<T>
    {
        return encoder.Decode(reader);
    }
}

public readonly struct TypedEncoder : ITypedEncoder<TemporaryString>, ITypedEncoder<ArraySegment<byte>>, ITypedEncoder<TemporaryArray<byte>>
{
    public static readonly TypedEncoder Default = new();

    #region ArraySegment<byte>
    async ValueTask<ArraySegment<byte>> ITypedEncoder<ArraySegment<byte>>.Decode(XmlReader reader)
    {
        var pool = ArrayPool<byte>.Instance;
        var buffer = pool.Rent(1024);
        try
        {
            var stream = new MemoryStream();
            while(await reader.ReadContentAsBase64Async(buffer, 0, buffer.Length) is > 0 and var read)
            {
                stream.Write(buffer, 0, read);
            }
            if(!stream.TryGetBuffer(out var result))
            {
                result = new(stream.ToArray());
            }
            return result;
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    ValueTask ITypedEncoder<ArraySegment<byte>>.Encode(ArraySegment<byte> value, XmlWriter writer)
    {
        return new(writer.WriteBase64Async(value.Array!, value.Offset, value.Count));
    }
    #endregion

    #region TemporaryString
    static readonly TemporaryString.AsynchronousReader<XmlReader> xmlTemporaryStringReader = static async (buffer, reader) => {
        return await reader.ReadValueChunkAsync(buffer.Array!, buffer.Offset, buffer.Count);
    };

    async ValueTask<TemporaryString> ITypedEncoder<TemporaryString>.Decode(XmlReader reader)
    {
        var str = new TemporaryString(arraySource: ArraySource<char>.Instance);
        try
        {
            await str.ReadFromAsync(xmlTemporaryStringReader, reader);
            await reader.ReadAsync();
            return str;
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            throw;
        }

        bool Dispose()
        {
            str.Dispose();
            return false;
        }
    }

    async ValueTask ITypedEncoder<TemporaryString>.Encode(TemporaryString value, XmlWriter writer)
    {
        var segment = value.Value;
        await writer.WriteCharsAsync(segment.Array!, segment.Offset, segment.Count);
    }
    #endregion

    #region TemporaryArray<byte>
    static readonly TemporaryArray<byte>.AsynchronousReader<XmlReader> xmlTemporaryByteArrayReader = static async (buffer, reader) => {
        return await reader.ReadContentAsBase64Async(buffer.Array!, buffer.Offset, buffer.Count);
    };

    async ValueTask<TemporaryArray<byte>> ITypedEncoder<TemporaryArray<byte>>.Decode(XmlReader reader)
    {
        var arr = new TemporaryArray<byte>(arraySource: ArraySource<byte>.Instance);
        try
        {
            await arr.ReadFromAsync(xmlTemporaryByteArrayReader, reader);
            await reader.ReadAsync();
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

    async ValueTask ITypedEncoder<TemporaryArray<byte>>.Encode(TemporaryArray<byte> value, XmlWriter writer)
    {
        var segment = value.Value;
        await writer.WriteBase64Async(segment.Array!, segment.Offset, segment.Count);
    }
    #endregion

    static class ArrayPool<T>
    {
        public static readonly System.Buffers.ArrayPool<T> Instance = System.Buffers.ArrayPool<T>.Create();
    }

    sealed class ArraySource<T> : TemporaryArraySource<T> where T : unmanaged
    {
        public static readonly ArraySource<T> Instance = new();

        private ArraySource() : base(ArrayPool<T>.Instance)
        {

        }

        public override void ZeroMemory(Span<T> span)
        {
#if NETSTANDARD2_1_OR_GREATER
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(MemoryMarshal.Cast<T, byte>(span));
#else
            base.ZeroMemory(span);
#endif
        }
    }
}
