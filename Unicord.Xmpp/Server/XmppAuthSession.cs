using System.IO;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Server;

public abstract class XmppAuthSession(Stream stream) : XmppStreamSession(stream)
{
    protected async sealed override ValueTask Authenticated()
    {
        // New session state over the same stream
        Initialize(Stream);
    }
}
