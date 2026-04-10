using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unicord.Primitives.Json.Handlers;

namespace Unicord.Xrd.Protocol.Handlers;

/// <summary>
/// Provides a handler with empty implementation of all handler methods.
/// </summary>
public partial class NullHandler : IAsyncDisposable, IPayloadHandler
{
    public static readonly NullHandler Instance = new();

    protected NullHandler()
    {

    }

    ValueTask IPayloadHandler.Other(JsonReader payloadReader)
    {
        return default;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return default;
    }
}
