using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal abstract class WebSocketStream : NonSeekableStream
{
    public WebSocket WebSocket { get; }

    public sealed override bool CanRead => true;
    public override bool CanWrite => true;
    public sealed override bool CanTimeout => false;

    public WebSocketCloseStatus ClosingStatus { get; set; } = WebSocketCloseStatus.NormalClosure;
    public string? ClosingDescription { get; set; }

    public required WebSocketMessageType SendingMessageType { get; init; } = WebSocketMessageType.Binary;

    public WebSocketStream(WebSocket webSocket)
    {
        WebSocket = webSocket;
    }

    public static WebSocketStream Create(WebSocket webSocket, WebSocketMessageType messageType, ArrayPool<byte>? bufferPool = null)
    {
        return bufferPool != null ? new WebSocketBufferedStream(webSocket, bufferPool)
        {
            SendingMessageType = messageType
        } : new WebSocketUnbufferedStream(webSocket)
        {
            SendingMessageType = messageType
        };
    }

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        // Do not use the ArraySegment version to have a struct result
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public async sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
        return result.Count;
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public abstract override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
    public abstract override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

    public sealed override void Flush()
    {
        FlushAsync().GetAwaiter().GetResult();
    }

    public abstract override Task FlushAsync(CancellationToken cancellationToken = default);

    public sealed override void Close()
    {
        CloseAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        await WebSocket.CloseAsync(ClosingStatus, ClosingDescription, cancellationToken);
    }

    protected sealed override void Dispose(bool disposing)
    {
        if(disposing)
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public async sealed override ValueTask DisposeAsync()
    {
        await FlushAsync();
        await WebSocket.CloseOutputAsync(ClosingStatus, ClosingDescription, default);
    }
}
