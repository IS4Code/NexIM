using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Communication;

internal class Compression : BaseCompressionHandler, ICommandHandler
{
    CompressionMethod? method;

    public CommandState State { get; init; }

    protected async override ValueTask<bool> OnMethod(Token<CompressionMethod>? name)
    {
        this.SetOnce(ref method, name?.ToEnum());
        return true;
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        var session = State.Session;

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
