using NexIM.Primitives;
using NexIM.Server.Accounts;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace NexIM.Server;

partial class NexServer
{
    public IRemoteProvider GetUploadedFileProvider(Guid identifier)
    {
        return new UploadedFileGuidProvider(identifier, this);
    }

    public IRemoteProvider GetUploadedFileBySha1Provider(ArraySegment<byte> hash)
    {
        if(hash.Count == 0)
        {
            return EmptyUploadedFileProvider.Instance;
        }
        return new UploadedFileSha1Provider(hash, this);
    }

    sealed class UploadedFileGuidProvider(Guid identifier, NexServer server) : IdentifierRemoteProvider<TemporaryFile, UploadedFile, Guid>
    {
        protected override Guid Identifier => identifier;
        protected override MemberInfo IdentifierMember => identifierMember;

        protected override ValueTask<UploadedFile?> Load(CancellationToken cancellationToken)
        {
            return server.FindUploadedFile(Identifier, cancellationToken);
        }

        static readonly MemberInfo identifierMember =
            ((MemberExpression)((Expression<Func<UploadedFile, Guid>>)(x => x.Identifier)).Body).Member;

        public override bool References(UploadedFile? other) => Identifier == other?.Identifier;
    }

    sealed class UploadedFileSha1Provider(ArraySegment<byte> hash, NexServer server) : IdentifierRemoteProvider<TemporaryFile, UploadedFile, ArraySegment<byte>>
    {
        protected override ArraySegment<byte> Identifier => hash;
        protected override MemberInfo IdentifierMember => identifierMember;

        protected override ValueTask<UploadedFile?> Load(CancellationToken cancellationToken)
        {
            return server.FindUploadedFileBySha1(hash, cancellationToken);
        }

        static readonly MemberInfo identifierMember =
            ((MemberExpression)((Expression<Func<UploadedFile, ArraySegment<byte>>>)(x => x.Sha1Hash)).Body).Member;

        public bool Equals(UploadedFileSha1Provider other)
        {
            return hash.AsSpan().SequenceEqual(other.Identifier.AsSpan());
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.AddBytes(hash.AsSpan());
            return hashCode.ToHashCode();
        }

        public override bool Equals(IRemoteProvider obj) => obj is UploadedFileSha1Provider other && Equals(other);
        public override bool References(UploadedFile? other) => other != null && other.Sha1Hash.AsSpan().SequenceEqual(hash.AsSpan());
    }

    sealed class EmptyUploadedFileProvider : DerivedRemoteProvider<TemporaryFile, UploadedFile>
    {
        public static readonly EmptyUploadedFileProvider Instance = new();

        private EmptyUploadedFileProvider()
        {

        }

        protected override ValueTask<TResult>? TryGetImmediate<TResult>(LambdaExpression retrieveExpression, CancellationToken cancellationToken)
        {
            // All properties are empty
            return new(default(TResult)!);
        }

        public override bool Equals(IRemoteProvider other) => other == Instance;
        public override int GetHashCode() => 0;
        public override bool References(UploadedFile? other) => false;
        protected override ValueTask<UploadedFile?> Load(CancellationToken cancellationToken) => default;
    }
}
