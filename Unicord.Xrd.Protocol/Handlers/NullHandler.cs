using System;
using System.Threading.Tasks;

namespace Unicord.Xrd.Protocol.Handlers;

/// <summary>
/// Provides a handler with empty implementation of all handler methods.
/// </summary>
public partial class NullHandler : IAsyncDisposable
{
    public static readonly NullHandler Instance = new();

    protected NullHandler()
    {

    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return default;
    }
}
