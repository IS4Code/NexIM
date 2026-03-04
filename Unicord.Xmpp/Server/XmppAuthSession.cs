using System.IO;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/>
/// that handles SASL authentication.
/// </summary>
public abstract class XmppAuthSession(Stream stream) : XmppStreamSession(stream)
{
    protected async sealed override ValueTask Authenticated()
    {
        // New session state over the same stream
        Initialize(Stream);
    }
}
