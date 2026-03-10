using Unicord.Xmpp.Protocol.Grammar;

namespace Unicord.Xmpp.Protocol.Handlers;

public interface IPayloadHandlerContext
{
    Decoder Decoder { get; }
    string DefaultNamespace { get; }
}
