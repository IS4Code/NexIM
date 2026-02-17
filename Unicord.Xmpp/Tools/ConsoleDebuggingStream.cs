using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal sealed class ConsoleDebuggingStream : Stream
{
    readonly Stream inner;

    readonly MemoryStream readStream = new();
    readonly MemoryStream writeStream = new();

    StreamReader? readReader, writeReader;

    public ConsoleDebuggingStream(Stream inner)
    {
        this.inner = inner;
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanWrite => inner.CanWrite;
    public override bool CanSeek => false;
    public override bool CanTimeout => inner.CanTimeout;
    public override int ReadTimeout { get => inner.ReadTimeout; set => inner.ReadTimeout = value; }
    public override int WriteTimeout { get => inner.WriteTimeout; set => inner.WriteTimeout = value; }
    public override long Length => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    static readonly char[] trimChars = { '\n', '\r' };

    private void OnData(ReadOnlySpan<byte> data, MemoryStream stream, ref StreamReader? reader, ConsoleColor highlight)
    {
        if(data.Length == 0)
        {
            return;
        }

        stream.Write(data);
        stream.Position = 0;

        if(reader == null)
        {
            reader = new(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        }

        while(reader.ReadLine() is { } line)
        {
        reset:
            line = line.Trim(trimChars);
            if(stream.Position >= stream.Length)
            {
                // Read to the end
                var nextLine = reader.ReadLine();
                if(nextLine == null)
                {
                    reader.DiscardBufferedData();

                    if(line.EndsWith('>'))
                    {
                        // Ends a tag - safe to report

                        Reset();

                        OnLine(line, highlight);
                        return;
                    }

                    // This was the last line, but it might have been longer - put back

                    using var writer = new StreamWriter(stream, encoding: reader.CurrentEncoding, leaveOpen: true);

                    // Write out any BOM (will be ignored)
                    writer.Write('\0');
                    writer.Flush();

                    Reset();

                    writer.Write(line);
                    return;
                }

                // Not the last line (reader's buffer contains more data)
                OnLine(line, highlight);

                // Use the current line as next
                line = nextLine;
                goto reset;
            }

            OnLine(line, highlight);
        }

        Reset();

        void Reset()
        {
            stream.Position = 0;
            stream.SetLength(0);
        }
    }

    private void OnLine(string line, ConsoleColor highlight)
    {
        if(String.IsNullOrWhiteSpace(line))
        {
            return;
        }
        lock(typeof(Console))
        {
            var color = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = highlight;
                Console.WriteLine(line);
            }
            finally
            {
                Console.ForegroundColor = color;
            }
        }
    }

    private void OnWrite(ReadOnlySpan<byte> data)
    {
        OnData(data, writeStream, ref writeReader, ConsoleColor.Green);
    }

    private void OnRead(ReadOnlySpan<byte> data)
    {
        OnData(data, readStream, ref readReader, ConsoleColor.Yellow);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = inner.Read(buffer, offset, count);
        OnRead(buffer.AsSpan(offset, count));
        return count;
    }

    public override int Read(Span<byte> buffer)
    {
        int count = inner.Read(buffer);
        OnRead(buffer.Slice(0, count));
        return count;
    }

    public override int ReadByte()
    {
        int result = inner.ReadByte();
        if(result != -1)
        {
            Span<byte> data = stackalloc byte[1] { (byte)result };
            OnRead(data);
        }
        return result;
    }

    public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        count = await inner.ReadAsync(buffer, offset, count, cancellationToken);
        OnRead(buffer.AsSpan(offset, count));
        return count;
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int count = await inner.ReadAsync(buffer, cancellationToken);
        OnRead(buffer.Span.Slice(0, count));
        return count;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        OnWrite(buffer.AsSpan(offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        OnWrite(buffer);
        inner.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        Span<byte> data = stackalloc byte[1] { value };
        OnWrite(data);
        inner.WriteByte(value);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        OnWrite(buffer.AsSpan(offset, count));
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        OnWrite(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        var task = ReadAsync(buffer, offset, count);
        var tcs = new TaskCompletionSource<int>(state);
        task.ContinueWith(t => {
            if(t.IsFaulted)
            {
                tcs.TrySetException(t.Exception.InnerExceptions);
            }
            else if(t.IsCanceled)
            {
                tcs.TrySetCanceled();
            }
            else
            {
                tcs.TrySetResult(t.Result);
            }

            callback?.Invoke(tcs.Task);
        }, TaskScheduler.Default);
        return tcs.Task;
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return ((Task<int>)asyncResult).GetAwaiter().GetResult();
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        var task = WriteAsync(buffer, offset, count);
        var tcs = new TaskCompletionSource(state);
        task.ContinueWith(t => {
            if(t.IsFaulted)
            {
                tcs.TrySetException(t.Exception.InnerExceptions);
            }
            else if(t.IsCanceled)
            {
                tcs.TrySetCanceled();
            }
            else
            {
                tcs.TrySetResult();
            }

            callback?.Invoke(tcs.Task);
        }, TaskScheduler.Default);
        return tcs.Task;
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        ((Task)asyncResult).GetAwaiter().GetResult();
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return inner.FlushAsync(cancellationToken);
    }

    public override void Close()
    {
        readReader?.Close();
        writeReader?.Close();
        base.Close();
    }
}
