using System;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Xmpp.Protocol.Handlers;

public class DelegatingPayloadHandler<TDisposable>(IPayloadHandler inner, IAsyncDisposable disposable) : IPayloadHandler where TDisposable : IAsyncDisposable
{
    ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        return inner.Other(payloadReader);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await inner.DisposeAsync();
        }
        finally
        {
            await disposable.DisposeAsync();
        }
    }
}
