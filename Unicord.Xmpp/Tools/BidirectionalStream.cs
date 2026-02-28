using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal sealed class BidirectionalStream(Stream input, Stream output) : NonSeekableStream
{
    public override bool CanRead => input.CanRead;
    public override bool CanWrite => output.CanWrite;
    public override bool CanTimeout => input.CanTimeout && output.CanTimeout;

    public override int ReadTimeout {
        get => input.ReadTimeout;
        set => input.ReadTimeout = value;
    }

    public override int WriteTimeout {
        get => output.WriteTimeout;
        set => output.WriteTimeout = value;
    }

    public override void Flush()
    {
        output.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return output.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return input.Read(buffer, offset, count);
    }

    public override int ReadByte()
    {
        return input.ReadByte();
    }

    public override int Read(Span<byte> buffer)
    {
        return input.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return input.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return input.ReadAsync(buffer, cancellationToken);
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return input.BeginRead(buffer, offset, count, callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return input.EndRead(asyncResult);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        input.CopyTo(destination, bufferSize);
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        return input.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        output.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        output.WriteByte(value);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        output.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return output.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return output.WriteAsync(buffer, cancellationToken);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return output.BeginWrite(buffer, offset, count, callback, state);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        output.EndWrite(asyncResult);
    }

    public override void Close()
    {
        try
        {
            input.Close();
        }
        finally
        {
            output.Close();
        }
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            try
            {
                input.Dispose();
            }
            finally
            {
                output.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            await input.DisposeAsync();
        }
        finally
        {
            await output.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
