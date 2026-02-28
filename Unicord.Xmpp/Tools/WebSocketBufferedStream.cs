using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal sealed class WebSocketBufferedStream(WebSocket webSocket, ArrayPool<byte> bufferPool) : WebSocketStream(webSocket)
{
    byte[]? flushArray;
    int flushCount;

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int count = buffer.Length;
        if(count == 0)
        {
            return;
        }

        // Flush previous data
        await FlushAsync(cancellationToken, false);

        // Preserve data to be sent
        flushArray = bufferPool.Rent(count);
        flushCount = count;
        buffer.Span.CopyTo(flushArray.AsSpan());
    }

    public override Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return FlushAsync(cancellationToken, true).AsTask();
    }

    private async ValueTask FlushAsync(CancellationToken cancellationToken, bool endOfMessage)
    {
        if(flushArray != null)
        {
            // Send previous unflushed message
            try
            {
                await WebSocket.SendAsync(new ArraySegment<byte>(flushArray, 0, flushCount), SendingMessageType, endOfMessage, cancellationToken);
            }
            finally
            {
                bufferPool.Return(flushArray);
                flushArray = null;
                flushCount = 0;
            }
        }
    }
}
