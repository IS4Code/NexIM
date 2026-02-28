using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal abstract class AsyncStream : Stream
{
    public abstract override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    public abstract override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return TaskToAsyncResult.Begin(ReadAsync(buffer, offset, count), callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return TaskToAsyncResult.End<int>(asyncResult);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count), callback, state);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        TaskToAsyncResult.End(asyncResult);
    }
}
