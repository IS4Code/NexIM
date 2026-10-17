using System;
using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Tools;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IIrcSession"/> capable of
/// receiving and sending synchronized IRC messages in text stream.
/// </summary>
public abstract class IrcTextSession : IrcSession
{
    const int maxTagsLength = 8191;
    const int maxTagsDataLength = 4094;
    const int maxLineLength = 512;
    const int maxLineWithTagsLength = maxTagsLength + maxLineLength;
    const int maxParameterCount = 15;

    bool tagsSupported => (Capabilities & Capabilities.MessageTags) != 0;

    protected abstract PipeReader Reader { get; }
    protected abstract PipeWriter Writer { get; }

    readonly SemaphoreSlim semaphore = new(1, 1);
    protected override ValueTask SendCommand(in CommandMessage message)
    {
        var semaphoreTask = semaphore.WaitAsync(CancellationToken);
        if(semaphoreTask.IsCompletedSuccessfully)
        {
            ValueTask<FlushResult> writeTask;
            try
            {
                writeTask = Write(message);
            }
            catch(Exception e)
            {
                // Synchronous exception
                semaphore.Release();
                return ValueTask.FromException(e);
            }
            if(writeTask.IsCompletedSuccessfully)
            {
                semaphore.Release();
                return default;
            }
            return WriteAsync();
            async ValueTask WriteAsync()
            {
                try
                {
                    await writeTask;
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        var copy = message;
        return SendAsync();
        async ValueTask SendAsync()
        {
            await semaphoreTask;
            try
            {
                await Write(copy);
            }
            finally
            {
                semaphore.Release();
            }
        }

        ValueTask<FlushResult> Write(in CommandMessage message)
        {
            int total;
            try
            {
                int lineLength = maxLineLength;
                if(message.ContainsTags && tagsSupported)
                {
                    lineLength += maxTagsLength - 2 * maxTagsDataLength;
                    if(message.ServerTags != null)
                    {
                        lineLength += maxTagsDataLength;
                    }
                    if(message.ClientTags != null)
                    {
                        lineLength += maxTagsDataLength;
                    }
                }

                total = Serialize(message, Writer.GetSpan(lineLength));
                if(total > lineLength)
                {
                    // TODO Best effort at splitting
                    return default;
                }
            }
            catch(IndexOutOfRangeException)
            {
                return default;
            }
            catch(ArgumentOutOfRangeException)
            {
                return default;
            }

            Writer.Advance(total);
            return Writer.FlushAsync(CancellationToken);
        }

        int Serialize(in CommandMessage message, Span<byte> span)
        {
            ReadOnlySpan<byte> originalSpan = span;

            if(message.ContainsTags && tagsSupported)
            {
                span.Write((byte)'@');

                if(message.ServerTags is { } serverTags)
                {
                    WriteTags(ref span, serverTags, ""u8);
                }
                if(message.ClientTags is { } clientTags)
                {
                    WriteTags(ref span, clientTags, "+"u8);
                }

                span.Write((byte)' ');
            }

            if(!message.Source.IsEmpty)
            {
                span.Write((byte)':');
                WritePlainValue(ref span, message.Source.Value, invalidInMiddle);
                span.Write((byte)' ');
            }

            span.Write(message.Command);

            int paramIndex = 0;
            var parameters = message.Parameters;
            foreach(var param in parameters)
            {
                paramIndex++;
                span.Write((byte)' ');

                var value = param.AsSpan();

                if(paramIndex == parameters.Count)
                {
                    span.Write((byte)':');
                    WriteTrailingParameter(ref span, value);
                }
                else
                {
                    WriteMiddleParameter(ref span, value);
                }
            }

            // EOL
            span.Write("\r\n"u8);

            // TODO Check lengths of individual sections
            originalSpan.Overlaps(span, out var written);
            return written;
        }
    }

    static readonly SearchValues<byte> invalidInMiddle = SearchValues.Create(" \0\r\n"u8);
    private static void WriteMiddleParameter(ref Span<byte> span, ReadOnlySpan<byte> value)
    {
        if(value.IsEmpty)
        {
            // Placeholder for empty parameters
            span.Write("''"u8);
            return;
        }

        if(value[0] == (byte)':')
        {
            // Prefix to avoid reserved colon
            span.Write("\\:"u8);
            value = value.Slice(1);
        }

        WritePlainValue(ref span, value, invalidInMiddle);
    }

    static readonly SearchValues<byte> invalidInTrailing = SearchValues.Create("\0\r\n"u8);
    private static void WriteTrailingParameter(ref Span<byte> span, ReadOnlySpan<byte> value)
    {
        WritePlainValue(ref span, value, invalidInTrailing);
    }

    private static void WritePlainValue(ref Span<byte> span, ReadOnlySpan<byte> value, SearchValues<byte> invalid)
    {
        for(int next = value.IndexOfAny(invalid); next != -1; next = value.IndexOfAny(invalid))
        {
            span.Write(value.Slice(0, next));
            span.Write(value[next] switch {
                (byte)'\0' => @"\0"u8,
                (byte)'\r' => @"\r"u8,
                (byte)'\n' => @"\n"u8,
                _ => @"_"u8
            });
            value = value.Slice(next + 1);
        }
        span.Write(value);
    }

    private static void WriteTags(ref Span<byte> span, in NonEmptyDictionary<bstring, bstring> tags, ReadOnlySpan<byte> keyPrefix)
    {
        bool first = true;
        foreach(var (tagKey, tagValue) in tags)
        {
            if(first)
            {
                first = false;
            }
            else
            {
                span.Write((byte)',');
            }
            span.Write(keyPrefix);
            WriteTagValue(ref span, tagKey);
            if(!tagValue.IsEmpty)
            {
                span.Write((byte)'=');
                WriteTagValue(ref span, tagValue);
            }
        }
    }

    static readonly SearchValues<byte> invalidInTag = SearchValues.Create(" ;\\\0\r\n"u8);
    private static void WriteTagValue(ref Span<byte> span, ReadOnlySpan<byte> value)
    {
        for(int next = value.IndexOfAny(invalidInTag); next != -1; next = value.IndexOfAny(invalidInTag))
        {
            span.Write(value.Slice(0, next));
            span.Write(value[next] switch {
                (byte)' ' => @"\s"u8,
                (byte)';' => @"\:"u8,
                (byte)'\\' => @"\\"u8,
                (byte)'\r' => @"\r"u8,
                (byte)'\n' => @"\n"u8,
                _ => ""u8
            });
            value = value.Slice(next + 1);
        }
        span.Write(value);
    }

    static readonly byte[] allNumerics = GetAllNumerics();
    private bstring GetReplyCommand(MessageReply reply)
    {
        int numeric = (int)reply;
        if(numeric < 0 || numeric >= 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(reply));
        }
        return allNumerics.AsMemory(numeric * 3, 3);
    }

    static byte[] GetAllNumerics()
    {
        var array = new byte[3 * 1000];
        for(int i = 0; i < 1000; i++)
        {
            var span = array.AsSpan(i * 3, 3);
            i.TryFormat(span, out _, "000", CultureInfo.InvariantCulture);
        }
        return array;
    }

    protected override ValueTask SendReply(in ReplyMessage message)
    {
        return SendCommand(new(
            Command: GetReplyCommand(message.Reply),
            Parameters: message.Parameters,
            Source: message.Source,
            ServerTags: message.ServerTags,
            ClientTags: message.ClientTags
        ));
    }

    protected abstract ValueTask OnCommand(in CommandMessage message);
    protected abstract ValueTask OnReply(in ReplyMessage message);

    public async Task ReadMessages(CancellationToken cancellationToken)
    {
        try
        {
            long examined = 0;
            ReadResult result;
            do
            {
                result = await Reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                while(buffer.Slice(examined).PositionOf((byte)'\n') is { } lineEnd)
                {
                    // Line end detected
                    var line = buffer.Slice(buffer.Start, lineEnd);

                    // Move buffer past newline
                    buffer = buffer.Slice(buffer.GetPosition(1, lineEnd));
                    examined = 0;

                    if(line.Length == 0)
                    {
                        // Empty line ignored
                        continue;
                    }

                    if(line.Length + 1 >= (line.FirstSpan[0] == (byte)'@' ? maxLineWithTagsLength : maxLineLength))
                    {
                        // Individual line exceeds length
                        return;
                    }

                    var lastPos = line.GetPosition(line.Length - 1);
                    if(line.Slice(lastPos).FirstSpan[0] == (byte)'\r')
                    {
                        // Strip CR
                        line = line.Slice(line.Start, lastPos);
                    }

                    await OnMessageLine(line);
                }

                // Buffer is unfinished line
                examined = buffer.Length;

                if(examined > 0 && examined > (buffer.FirstSpan[0] == (byte)'@' ? maxLineWithTagsLength : maxLineLength))
                {
                    // Growing without newline
                    return;
                }

                Reader.AdvanceTo(buffer.Start, buffer.End);
            }
            while(!result.IsCompleted);
        }
        finally
        {
            await Reader.CompleteAsync();
        }
    }

    static readonly ArrayPool<byte> messagePool = ArrayPool<byte>.Create();
    private protected static readonly ArrayPool<bstring> parameterPool = ArrayPool<bstring>.Create();

    private async ValueTask OnMessageLine(ReadOnlySequence<byte> lineSequence)
    {
        byte[]? rented = null;
        int length = (int)lineSequence.Length;
        var line = lineSequence.IsSingleSegment ? lineSequence.First : (rented = messagePool.Rent(length)).AsMemory(0, length);
        try
        {
            if(rented != null)
            {
                lineSequence.CopyTo(rented);
            }

            var message = ParseMessageLine(line, out var parameterBuffer);
            try
            {
                if(message.Command.IsEmpty)
                {
                    // Ignore bad command
                    return;
                }

                if(ParseNumeric(message.Command) is { } reply)
                {
                    await OnReply(new(
                        reply,
                        Parameters: message.Parameters,
                        Source: message.Source,
                        ServerTags: message.ServerTags,
                        ClientTags: message.ClientTags
                    ));
                }
                else
                {
                    await OnCommand(message);
                }
            }
            finally
            {
                if(parameterBuffer != null)
                {
                    // Clear references
                    parameterPool.Return(parameterBuffer, clearArray: true);
                }
            }
        }
        finally
        {
            if(rented != null)
            {
                messagePool.Return(rented);
            }
        }
    }

    private MessageReply? ParseNumeric(ReadOnlySpan<byte> command)
    {
        if(command.Length == 3 && command.IndexOfAnyExceptInRange((byte)'0', (byte)'9') == -1)
        {
            return (MessageReply)Int32.Parse(command, CultureInfo.InvariantCulture);
        }
        return null;
    }

    private CommandMessage ParseMessageLine(ReadOnlyMemory<byte> line, out bstring[]? parameterBuffer)
    {
        // Non-empty
        var span = line.Span;
        parameterBuffer = null;

        NonEmptyDictionary<bstring, bstring>? serverTags, clientTags;
        if(span.TryMatchStart((byte)'@'))
        {
            if(!span.TrySplitAfter((byte)' ', out var tagsSpan))
            {
                // Invalid message
                return default;
            }

            var serverBuilder = new NonEmptyDictionary<bstring, bstring>.Builder();
            var clientBuilder = new NonEmptyDictionary<bstring, bstring>.Builder();
            while(tagsSpan.Length > 0)
            {
                tagsSpan.TrySplitAfter((byte)';', out var tagSpan);
                tagSpan.TrySplitBefore((byte)'=', out var valueSpan);

                if(tagSpan.IsEmpty)
                {
                    // Empty name
                    continue;
                }

                if(tagSpan.TryMatchStart((byte)'+'))
                {
                    clientBuilder[new(line, tagSpan)] = new(line, valueSpan);
                }
                else
                {
                    serverBuilder[new(line, tagSpan)] = new(line, valueSpan);
                }
            }
            serverTags = serverBuilder.TryToDictionary();
            clientTags = clientBuilder.TryToDictionary();
        }
        else
        {
            serverTags = default;
            clientTags = default;
        }

        IrcAddress source;
        if(span.TryMatchStart((byte)':'))
        {
            if(!span.TrySplitAfter((byte)' ', out var sourceSpan))
            {
                // Invalid message
                return default;
            }
            source = new(new bstring(line, sourceSpan));
        }
        else
        {
            source = default;
        }

        MessageParameters parameters;
        if(span.TrySplitAfter((byte)' ', out var commandSpan))
        {
            // Parameters
            var rented = parameterPool.Rent(maxParameterCount);
            parameterBuffer = rented;
            try
            {
                parameters = ParseParameters(line, span, parameterBuffer);
            }
            catch when(Cleanup())
            {
                // Return rented array on exception
                throw;
            }

            bool Cleanup()
            {
                parameterPool.Return(rented, clearArray: true);
                return false;
            }
        }
        else
        {
            parameters = default;
        }

        return new(
            new(line, commandSpan),
            Source: source,
            Parameters: parameters,
            ServerTags: serverTags,
            ClientTags: clientTags
        );
    }

    static MessageParameters ParseParameters(ReadOnlyMemory<byte> line, ReadOnlySpan<byte> span, bstring[] buffer)
    {
        int count = 0;

        while(count < maxParameterCount)
        {
            if(!span.StripStart((byte)' '))
            {
                // Ignore starting space in middle parameter (not strictly allowed)
                break;
            }

            // Parameter starts
            if(span.TryMatchStart((byte)':') || count == maxParameterCount - 1)
            {
                // Trailing parameter - rest is last parameter
                buffer[count++] = new(line, span);
                break;
            }

            // More parameters follow
            span.TrySplitAfter((byte)' ', out var param);
            buffer[count++] = new(line, param);
        }

        return new(buffer, count);
    }
}
