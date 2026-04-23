using System;
using System.IO;

namespace Unicord.Tools;

public abstract class NonSeekableStream : Stream
{
    public sealed override bool CanSeek => false;
    public sealed override long Length => throw new NotSupportedException();
    public sealed override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public sealed override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public sealed override void SetLength(long value)
    {
        throw new NotSupportedException();
    }
}
