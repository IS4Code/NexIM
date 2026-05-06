using System;
using System.IO;
using System.Security.Cryptography;
using NexIM.Primitives;

namespace NexIM.Server.Accounts;

public sealed class UploadedFile : TemporaryFile
{
    public required Guid Identifier { get; init; }
    public required DateTimeOffset Uploaded { get; init; }
    // TODO Multiple hashes
    public required ArraySegment<byte> Sha1Hash { get; init; }
    public required ArraySegment<byte> Sha256Hash { get; init; }

    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset? Expires { get; set; }

    // Null if account no longer exists
    internal Account? Uploader { get; init; }

    internal UploadedFile()
    {
        // Ownership of the file is not tied to GC
        GC.SuppressFinalize(this);
    }

    internal UploadedFile(TemporaryFile source) : base(source)
    {
        GC.SuppressFinalize(this);
    }

    public override TemporaryFile MoveFrom()
    {
        // The instance is non-destructive
        return this;
    }

    internal static UploadedFile MoveFrom(TemporaryFile original, Account uploader)
    {
        if(original is UploadedFile file)
        {
            // Already uploaded
            return file;
        }

        var date = IdentifierHelper.IdentifierTimeNow;

        byte[] sha1, sha256;

        using(var stream = new FileStream(original.FilePath, FileMode.Open))
        {
            sha1 = SHA1.HashData(stream);
            stream.Position = 0;
            sha256 = SHA256.HashData(stream);
        }

        return new UploadedFile(original) {
            Identifier = IdentifierHelper.CreateGuid(date),
            Uploaded = date,
            Sha1Hash = sha1,
            Sha256Hash = sha256,
            Uploader = uploader
        };
    }

    protected override string CreatePath()
    {
        // Path based on SHA256 hash
        const string uploads = "uploads";
        Directory.CreateDirectory(uploads);
        return Path.Combine(uploads, Convert.ToHexStringLower(Sha256Hash));
    }
}
