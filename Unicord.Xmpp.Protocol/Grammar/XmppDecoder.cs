using System;
using System.Buffers;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Server.Primitives;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

public static partial class XmppDecoder
{
    public readonly record struct Result(bool Success, IPayloadHandler? InnerHandler);

    public static partial ValueTask<Result> DecodePayload(XmlReader reader, IPayloadHandler handler);

    private static partial async ValueTask EmptyElementTextAsync(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            return;
        }

        await reader.ReadAsync();
        if(reader.NodeType != XmlNodeType.EndElement)
        {
            throw new XmppException("Element was expected to be empty.", false);
        }
    }

    static async ValueTask<bool> OpenElement(XmlReader reader)
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
                throw new XmppException("Element was expected to have textual value.", false);
        }

        return true;
    }

    private static partial async ValueTask<string?> ReadElementStringAsync(XmlReader reader)
    {
        if(!await OpenElement(reader))
        {
            return null;
        }

        try
        {
            return await reader.ReadContentAsStringAsync();
        }
        finally
        {
            EnsureEndElement(reader);
        }
    }

    static readonly TemporaryString.AsynchronousReader<XmlReader> xmlTemporaryStringReader = static async (buffer, reader) => {
        return await reader.ReadValueChunkAsync(buffer.Array!, buffer.Offset, buffer.Count);
    };

    private static partial async ValueTask<TemporaryString?> ReadElementTemporaryStringAsync(XmlReader reader)
    {
        if(!await OpenElement(reader))
        {
            return null;
        }

        var str = new TemporaryString(arraySource: StringSource.Instance);
        try
        {
            await str.ReadFromAsync(xmlTemporaryStringReader, reader);
            try
            {
                await reader.ReadAsync();
                return str;
            }
            finally
            {
                EnsureEndElement(reader);
            }
        }
        catch when(Dispose())
        {
            // Dispose unreturned data immediately
            return null;
        }

        bool Dispose()
        {
            str.Dispose();
            return false;
        }
    }

    static void EnsureEndElement(XmlReader reader)
    {
        if(reader.NodeType != XmlNodeType.EndElement)
        {
            throw new XmppException("Element was expected to have textual value.", false);
        }
    }

    sealed class StringSource : TemporaryArraySource<char>
    {
        public static readonly StringSource Instance = new();

        private StringSource() : base(ArrayPool<char>.Create())
        {

        }

        public override void ZeroMemory(Span<char> span)
        {
#if NETSTANDARD2_1_OR_GREATER
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(MemoryMarshal.Cast<char, byte>(span));
#else
            base.ZeroMemory(span);
#endif
        }
    }
}
