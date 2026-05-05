using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        return new(new FileProvider(identifier, server));
    }

    sealed class FileProvider : DerivedRemoteProvider<TemporaryFile, UploadedFile>, RemoteProvider<UploadedFile>.IResultRemoteProvider<Guid>
    {
        readonly Guid identifier;
        readonly Server server;

        public FileProvider(Guid identifier, Server server)
        {
            this.identifier = identifier;
            this.server = server;
        }

        protected override ValueTask<UploadedFile?> Load(CancellationToken cancellationToken)
        {
            return server.FindUploadedFile(identifier, cancellationToken);
        }

        static readonly MemberInfo identifierMember =
            ((MemberExpression)((Expression<Func<UploadedFile, Guid>>)(x => x.Identifier)).Body).Member;

        ValueTask<Guid>? IResultRemoteProvider<Guid>.TryGetImmediate(LambdaExpression retrieveExpression, CancellationToken cancellationToken)
        {
            var argument = retrieveExpression.Parameters[0];
            switch(retrieveExpression.Body)
            {
                case MemberExpression { Expression: var param, Member: var member } when argument.Equals(param) && identifierMember.Equals(member):
                    return new(identifier);
            }
            return null;
        }

        public bool Equals(FileProvider other)
        {
            return identifier == other.identifier && server == other.server;
        }

        public override bool Equals(IRemoteProvider obj) => obj is FileProvider other && Equals(other);
        public override int GetHashCode() => identifier.GetHashCode();
        public override bool References(UploadedFile? other) => identifier == other?.Identifier;
    }
}
