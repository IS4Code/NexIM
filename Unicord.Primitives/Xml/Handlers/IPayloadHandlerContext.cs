using System;

namespace Unicord.Primitives.Xml.Handlers;

public interface IPayloadHandlerContext
{
    string DefaultNamespace { get; }
}

public readonly record struct EmptyPayloadHandlerContext() : IPayloadHandlerContext
{
    public string DefaultNamespace => String.Empty;
}
