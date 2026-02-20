using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Provides support for decoding from XML.
/// </summary>
public abstract class XmlDecoder : IValueXmlDecoder<TemporaryString>, IValueXmlDecoder<ArraySegment<byte>>, IValueXmlDecoder<TemporaryArray<byte>>
{
    protected abstract void ThrowElementNotEmpty();
    protected abstract void ThrowElementNotSimple();

    protected async ValueTask EmptyElement(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            return;
        }

        await reader.ReadAsync();
        if(reader.NodeType != XmlNodeType.EndElement)
        {
            ThrowElementNotEmpty();
        }
    }

    protected async ValueTask<bool> OpenElement(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            // Known to be empty
            return false;
        }

        await reader.ReadAsync();
        switch(reader.NodeType)
        {
            case XmlNodeType.EndElement:
                return false;
            case XmlNodeType.Element:
                ThrowElementNotSimple();
                return false;
        }

        return true;
    }

    protected T CloseElement<T>(XmlReader reader, T result)
    {
        try
        {
            if(reader.NodeType != XmlNodeType.EndElement)
            {
                ThrowElementNotSimple();
            }
            return result;
        }
        catch when(Dispose())
        {
            throw;
        }

        bool Dispose()
        {
            (result as IDisposable)?.Dispose();
            return false;
        }
    }

    protected ValueTask<T> Decode<T, TDecoder>(XmlReader reader, TDecoder decoder) where TDecoder : IValueXmlDecoder<T>
    {
        return decoder.Decode(reader);
    }

    async ValueTask<ArraySegment<byte>> IValueXmlDecoder<ArraySegment<byte>>.Decode(XmlReader reader)
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

    static readonly TemporaryString.AsynchronousReader<XmlReader> xmlTemporaryStringReader = static async (buffer, reader) => {
        return await reader.ReadValueChunkAsync(buffer.Array!, buffer.Offset, buffer.Count);
    };

    async ValueTask<TemporaryString> IValueXmlDecoder<TemporaryString>.Decode(XmlReader reader)
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

    static readonly TemporaryArray<byte>.AsynchronousReader<XmlReader> xmlTemporaryByteArrayReader = static async (buffer, reader) => {
        return await reader.ReadContentAsBase64Async(buffer.Array!, buffer.Offset, buffer.Count);
    };

    async ValueTask<TemporaryArray<byte>> IValueXmlDecoder<TemporaryArray<byte>>.Decode(XmlReader reader)
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

public interface IValueXmlDecoder<T>
{
    ValueTask<T> Decode(XmlReader reader);
}
