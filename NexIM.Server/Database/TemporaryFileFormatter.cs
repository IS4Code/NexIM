using System;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Database;

[ExcludeFormatterFromSourceGeneratedResolver]
internal sealed class TemporaryFileFormatter(IFormatterResolver standardResolver, Server server) : IMessagePackFormatter<TemporaryFile?>
{
    readonly IMessagePackFormatter<Guid> guidFormatter = standardResolver.GetFormatterWithVerify<Guid>();

    public void Serialize(ref MessagePackWriter writer, TemporaryFile? value, MessagePackSerializerOptions options)
    {
        if(value == null)
        {
            writer.WriteNil();
            return;
        }
        if(value is not UploadedFile file)
        {
            throw new ArgumentException("Cannot save partially uploaded file.", nameof(value));
        }
        guidFormatter.Serialize(ref writer, file.Identifier, options);
    }

    public TemporaryFile? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.TryReadNil())
        {
            return null;
        }
        var identifier = guidFormatter.Deserialize(ref reader, options);
        return server.FindUploadedFile(identifier);
    }
}
