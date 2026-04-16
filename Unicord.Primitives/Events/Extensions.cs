using System.Buffers;

namespace Unicord.Primitives.Events;

public enum EventExtensionType : sbyte
{
    None,
    Xmpp
}

public interface IEventExtension
{
    EventExtensionType Type { get; }
    ReadOnlySequence<byte> Serialize();
}
