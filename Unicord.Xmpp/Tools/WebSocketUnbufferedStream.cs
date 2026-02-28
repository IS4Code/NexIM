using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal sealed class WebSocketUnbufferedStream(WebSocket webSocket) : WebSocketStream(webSocket)
{
    bool needsFlush;

    public async override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if(count == 0)
        {
            return;
        }

        try
        {
            await WebSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), SendingMessageType, false, cancellationToken);
        }
        finally
        {
            needsFlush = true;
        }
    }

    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if(buffer.Length == 0)
        {
            return;
        }

        try
        {
            await WebSocket.SendAsync(buffer, SendingMessageType, false, cancellationToken);
        }
        finally
        {
            needsFlush = true;
        }
    }

    public async override Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if(!needsFlush)
        {
            return;
        }
        try
        {
            await WebSocket.SendAsync(Memory<byte>.Empty, SendingMessageType, true, cancellationToken);
        }
        finally
        {
            needsFlush = false;
        }
    }
}
