using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NexIM.Primitives.Json.Handlers;

public interface IPayloadHandler : IAsyncDisposable
{
    ValueTask Other(JsonReader payloadReader);
}

public interface IPayloadHandler<TContext> : IPayloadHandler
{
    TContext? Context { get; init; }
}
