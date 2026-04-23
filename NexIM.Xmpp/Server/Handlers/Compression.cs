using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal class Compression : BaseCompressionHandler<ICommandContext>
{
    CompressionMethod? method;

    protected async override ValueTask OnMethod(Token<CompressionMethod>? name)
    {
        this.SetOnce(ref method, name?.ToEnum());
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        var session = this.GetSession();

        if(!session.CanCompress)
        {
            await using var failure = await session.CompressionFailure();
            await failure.SetupFailed();
            return;
        }

        if(method != CompressionMethod.ZLib)
        {
            await using var failure = await session.CompressionFailure();
            await failure.UnsupportedMethod();
            return;
        }

        await session.Compressed();
    }
}
