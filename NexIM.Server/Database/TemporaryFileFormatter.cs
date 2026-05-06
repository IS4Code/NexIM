using System;
using MessagePack;
using MessagePack.Formatters;
using NexIM.Primitives;
using NexIM.Server.Accounts;

namespace NexIM.Server.Database;

[ExcludeFormatterFromSourceGeneratedResolver]
internal sealed class TemporaryFileFormatter(IFormatterResolver standardResolver, Server server) : IMessagePackFormatter<Remote<TemporaryFile>?>
{
    readonly IMessagePackFormatter<Guid> guidFormatter = standardResolver.GetFormatterWithVerify<Guid>();

    public void Serialize(ref MessagePackWriter writer, Remote<TemporaryFile>? value, MessagePackSerializerOptions options)
    {
        if(value is null or { IsEmpty: true })
        {
            writer.WriteNil();
            return;
        }
        if(value.GetValueOrDefault().TryCast<UploadedFile>() is not { } remote)
        {
            throw new ArgumentException("Cannot save partially uploaded file.", nameof(value));
        }
        var identifierTask = remote.Get(static x => x.Identifier);
        if(!identifierTask.IsCompleted || identifierTask.GetAwaiter().GetResult() is not { } identifier)
        {
            throw new ArgumentException("A fully must uploaded file must have its identifier synchronously known.", nameof(value));
        }
        guidFormatter.Serialize(ref writer, identifier, options);
    }

    public Remote<TemporaryFile>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.TryReadNil())
        {
            return null;
        }
        var identifier = guidFormatter.Deserialize(ref reader, options);
        return new(server.GetUploadedFileProvider(identifier));
    }
}
