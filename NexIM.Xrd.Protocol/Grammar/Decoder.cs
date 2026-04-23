using System;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives.Xml;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xrd.Protocol.Grammar;

public partial class Decoder : XmlDecoder
{
    public readonly record struct Result(bool Success, IPayloadHandler? InnerHandler);

    public partial ValueTask<Result> DecodePayload(XmlReader reader, IPayloadHandler handler);

    public sealed override string GetDefaultNamespace(XmlNameTable nameTable)
    {
        return nameTable.Add(String.Empty);
    }

    protected override void ThrowElementNotEmpty()
    {
        throw new XmlException("Element was expected to be empty.");
    }

    protected override void ThrowElementNotSimple()
    {
        throw new XmlException("Element was expected to have textual value.");
    }
}
