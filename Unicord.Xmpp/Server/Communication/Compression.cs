using System.Threading.Tasks;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Compression : CommandHandler, ICompressionHandler
{
    CompressionMethod? method;

    public Compression(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask ICompressionHandler.Method(Token<CompressionMethod>? name)
    {
        SetOnce(ref method, name?.ToEnum());
    }

    public async override ValueTask DisposeAsync()
    {
        if(!Session.CanCompress)
        {
            await using var failure = await Session.CompressionFailure();
            await failure.SetupFailed();
            return;
        }

        if(method != CompressionMethod.ZLib)
        {
            await using var failure = await Session.CompressionFailure();
            await failure.UnsupportedMethod();
            return;
        }

        await Session.Compressed();
    }
}
