using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Server;

public abstract class XmppFrameSession(Stream stream) : XmppAuthSession(stream)
{
    protected async override ValueTask EndStream()
    {
        var writer = Writer;

        if(writer.WriteState != WriteState.Prolog)
        {
            // There are open elements - close them
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();
        }

        // Write closing frame
        await writer.WriteStartElementAsync(null, XmppVocabulary.Standard.Close.Value, XmppVocabulary.Standard.FramingNs.Value);
        await writer.WriteEndElementAsync();

        await writer.FlushAsync();
    }
}
