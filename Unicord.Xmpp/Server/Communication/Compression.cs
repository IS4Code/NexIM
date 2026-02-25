using System.Threading.Tasks;
using Unicord.Server.Primitives.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal class Compression : CommandHandler, ICompressionHandler
{
    string? method;

    public Compression(XmppServer server, IXmppSession session, string? identifier) : base(server, session, identifier)
    {

    }

    async ValueTask ICompressionHandler.Method(Token<CompressionMethod>? name)
    {
        SetOnce(ref method, name);
    }

    public async override ValueTask DisposeAsync()
    {
        if(!Session.CanCompress)
        {
            await using var failure = await Session.CompressionFailure();
            await failure.SetupFailed();
            return;
        }

        if(method != "zlib")
        {
            await using var failure = await Session.CompressionFailure();
            await failure.UnsupportedMethod();
            return;
        }

        await Session.Compressed();
    }
}
