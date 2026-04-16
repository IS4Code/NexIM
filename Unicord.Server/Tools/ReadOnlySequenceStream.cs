using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;

namespace Unicord.Server.Tools;

internal sealed class ReadOnlySequenceStream(ReadOnlySequence<byte> source) : Stream
{
#pragma warning disable CS9124 // Source is captured to provide length and seeking
    ReadOnlySequence<byte> remaining = source;
#pragma warning restore CS9124

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override long Length => source.Length;

    public override long Position {
        get => Length - remaining.Length;
        set => remaining = source.Slice(value);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if(remaining.Length < count)
        {
            count = (int)remaining.Length;
        }
        remaining.CopyTo(buffer.AsSpan(offset, count));
        remaining = remaining.Slice(count);
        return count;
    }

    public override int ReadByte()
    {
        foreach(var memory in remaining)
        {
            if(!memory.IsEmpty)
            {
                remaining = remaining.Slice(1);
                return memory.Span[0];
            }
        }
        return -1;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch(origin)
        {
            case SeekOrigin.Begin:
                break;
            case SeekOrigin.Current:
                offset += Position;
                break;
            case SeekOrigin.End:
                offset += Length;
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(origin), (int)origin, typeof(SeekOrigin));
        }

        return Position = offset;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void WriteByte(byte value)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }
}
