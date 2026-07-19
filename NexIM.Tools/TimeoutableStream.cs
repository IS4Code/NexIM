using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NexIM.Tools;

public class TimeoutableStream(Stream inner) : AsyncStream
{
    public override bool CanTimeout => true;
    public override int ReadTimeout { get; set; }
    public override int WriteTimeout { get; set; }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, WithTimeout(cancellationToken, ReadTimeout));
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, WithTimeout(cancellationToken, ReadTimeout));
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, WithTimeout(cancellationToken, WriteTimeout));
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, WithTimeout(cancellationToken, WriteTimeout));
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(WithTimeout(cancellationToken, WriteTimeout));

    private CancellationToken WithTimeout(CancellationToken source, int timeout)
    {
        if(timeout <= 0)
        {
            return source;
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(source);
        cts.CancelAfter(timeout);
        return cts.Token;
    }

    #region Non-timeouting synchronous implementation
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override int ReadByte() => inner.ReadByte();
    public override int Read(Span<byte> buffer) => inner.Read(buffer);
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override void WriteByte(byte value) => inner.WriteByte(value);
    public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Close() => inner.Close();
    public override void CopyTo(Stream destination, int bufferSize) => inner.CopyTo(destination, bufferSize);
    #endregion
}
