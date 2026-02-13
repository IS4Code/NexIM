using System;
using System.IO;

namespace Unicord.Xmpp.Tools;

internal sealed class DuplicatingStream : Stream
{
    readonly Stream first, second;

    public DuplicatingStream(Stream first, Stream second)
    {
        this.first = first;
        this.second = second;
    }

    public override bool CanWrite => first.CanWrite || second.CanWrite;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count)
    {
        first.Write(buffer, offset, count);
        second.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        first.WriteByte(value);
        second.WriteByte(value);
    }

    public override void Close()
    {
        first.Close();
        second.Close();
    }

    public override void Flush()
    {
        first.Flush();
        second.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }
}
