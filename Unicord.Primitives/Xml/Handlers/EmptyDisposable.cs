using System;
using System.Threading.Tasks;

namespace Unicord.Primitives.Xml.Handlers;

public readonly struct EmptyDisposable : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        return default;
    }
}
