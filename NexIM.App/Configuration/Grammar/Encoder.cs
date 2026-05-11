using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives.Xml;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration.Grammar;

public abstract partial class Encoder : XmlEncoder, IPayloadHandler
{
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract ValueTask<Encoder> ForkInner();

    public sealed override string DefaultNamespace => String.Empty;

    async ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        await Writer.WriteNodeWithLanguageAsync(payloadReader, false);
    }

    public virtual ValueTask DisposeAsync()
    {
        // Finalize element
        return new(Writer.WriteEndElementAsync());
    }
}
