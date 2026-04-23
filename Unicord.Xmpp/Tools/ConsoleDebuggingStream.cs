using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Tools;

namespace NexIM.Xmpp.Tools;

internal sealed class ConsoleDebuggingStream : NonSeekableStream
{
    readonly Stream inner;

    readonly MemoryStream readStream = new();
    readonly MemoryStream writeStream = new();

    readonly ConsoleColor color;

    StreamReader? readReader, writeReader;

    public ConsoleDebuggingStream(Stream inner, ConsoleColor color)
    {
        this.inner = inner;
        this.color = color;
    }

    public ConsoleDebuggingStream(Stream inner, object instance) : this(inner, ColorFromHash(instance.GetHashCode()))
    {

    }

    static readonly ConsoleColor[] hashColorSequence = {
        ConsoleColor.Cyan,
        ConsoleColor.Green,
        ConsoleColor.Magenta,
        ConsoleColor.Red,
        ConsoleColor.Yellow
    };

    static ConsoleColor ColorFromHash(int hash)
    {
        return hashColorSequence[Math.Abs(hash) % hashColorSequence.Length];
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanWrite => inner.CanWrite;
    public override bool CanTimeout => inner.CanTimeout;
    public override int ReadTimeout { get => inner.ReadTimeout; set => inner.ReadTimeout = value; }
    public override int WriteTimeout { get => inner.WriteTimeout; set => inner.WriteTimeout = value; }

    static readonly char[] trimChars = { '\n', '\r' };

    private void OnData(ReadOnlySpan<byte> data, MemoryStream stream, ref StreamReader? reader, bool outgoing)
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

                        OnLine(line, outgoing);
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
                OnLine(line, outgoing);

                // Use the current line as next
                line = nextLine;
                goto reset;
            }

            OnLine(line, outgoing);
        }

        Reset();

        void Reset()
        {
            stream.Position = 0;
            stream.SetLength(0);
        }
    }

    private void OnLine(string line, bool outgoing)
    {
        if(String.IsNullOrWhiteSpace(line))
        {
            return;
        }
        lock(typeof(Console))
        {
            var fg = Console.ForegroundColor;
            var bg = Console.BackgroundColor;
            try
            {
                if(outgoing)
                {
                    Console.BackgroundColor = bg;
                    Console.ForegroundColor = color;
                    Console.Write("<-");
                }
                else
                {
                    Console.BackgroundColor = color;
                    Console.ForegroundColor = bg;
                    Console.Write("->");
                    Console.BackgroundColor = bg;
                    Console.ForegroundColor = color;
                }

                Console.Write(" ");
                Console.WriteLine(line);
            }
            finally
            {
                Console.ForegroundColor = fg;
                Console.BackgroundColor = bg;
            }
        }
    }

    private void OnWrite(ReadOnlySpan<byte> data)
    {
        OnData(data, writeStream, ref writeReader, true);
    }

    private void OnRead(ReadOnlySpan<byte> data)
    {
        OnData(data, readStream, ref readReader, false);
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
