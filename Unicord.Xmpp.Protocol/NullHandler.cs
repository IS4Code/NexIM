using System;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol;

public sealed partial class NullHandler : IAsyncDisposable
{
    public static readonly NullHandler Instance = new();

    private NullHandler()
    {

    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return default;
    }
}
