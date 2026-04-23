using System;
using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Primitives.Events;
using NexIM.Server.Events;

namespace NexIM.Server.Database;

internal sealed partial class EventExtensionsFormatter : IMessagePackFormatter<EventExtensions>
{
    public static readonly EventExtensionsFormatter Instance = new();

    private EventExtensionsFormatter()
    {

    }

    public void Serialize(ref MessagePackWriter writer, EventExtensions value, MessagePackSerializerOptions options)
    {
        int count = value.Count;
        writer.WriteArrayHeader(count);

        foreach(var item in value)
        {
            writer.WriteExtensionFormat(SerializeObject(item));
        }
    }

    public EventExtensions Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        int count = reader.ReadArrayHeader();
        if(count == 0)
        {
            return default;
        }

        var builder = ImmutableHashSet<IEventExtension>.Empty.ToBuilder();

        for(int i = 0; i < count; i++)
        {
            if(DeserializeObject(reader.ReadExtensionFormat()) is { } obj)
            {
                builder.Add(obj);
            }
        }

        return new(builder.ToImmutable());
    }

    private ExtensionResult SerializeObject(IEventExtension obj)
    {
        return new((sbyte)obj.Type, obj.Serialize());
    }

    private IEventExtension? DeserializeObject(ExtensionResult ext)
    {
        var type = (EventExtensionType)ext.TypeCode;
        switch(type)
        {
            case EventExtensionType.None:
                // Skip
                return null;

            case EventExtensionType.Xmpp:
                return DeserializeXml(type, ext.Data);

            default:
                throw new ArgumentException($"Extension type {type} is not supported.", nameof(ext));
        }
    }
}
