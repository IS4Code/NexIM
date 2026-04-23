using System.Buffers;

namespace NexIM.Primitives.Events;

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
