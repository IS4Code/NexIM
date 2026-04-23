using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace NexIM.Xmpp.Tools;

internal sealed class WebSocketBufferedStream(WebSocket webSocket, ArrayPool<byte> bufferPool) : WebSocketStream(webSocket)
{
    byte[]? lastArray;
    int lastCount;

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

        // Send previous data
        await SendAsync(cancellationToken, false);

        // Preserve data to be sent
        lastArray = bufferPool.Rent(count);
        lastCount = count;
        buffer.Span.CopyTo(lastArray.AsSpan());
    }

    public async override Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if(lastArray != null)
        {
            // Send previous message but leave the array to indicate the message is not finished
            try
            {
                await WebSocket.SendAsync(new ArraySegment<byte>(lastArray, 0, lastCount), SendingMessageType, false, cancellationToken);
            }
            finally
            {
                lastCount = 0;
            }
        }
    }

    public override ValueTask SendAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(cancellationToken, true);
    }

    private async ValueTask SendAsync(CancellationToken cancellationToken, bool endOfMessage)
    {
        if(lastArray != null)
        {
            // Send previous message
            try
            {
                if(!endOfMessage && lastCount == 0)
                {
                    // Nothing to send
                    return;
                }
                await WebSocket.SendAsync(new ArraySegment<byte>(lastArray, 0, lastCount), SendingMessageType, endOfMessage, cancellationToken);
            }
            finally
            {
                bufferPool.Return(lastArray);
                lastArray = null;
                lastCount = 0;
            }
        }
    }
}
