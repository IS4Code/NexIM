using System;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol;

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
