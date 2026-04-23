using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NexIM.Primitives.Json.Handlers;

namespace NexIM.Xrd.Protocol.Handlers;

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
