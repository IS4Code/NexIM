using System;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol.Handlers;

public readonly struct EmptyDisposable : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        return default;
    }
}
