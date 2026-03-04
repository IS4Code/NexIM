using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using Xmpp = XmppVocabulary.Standard;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> using
/// the XMPP framing protocol.
/// </summary>
public abstract class XmppFrameSession(Stream stream) : XmppAuthSession(stream)
{
    protected async override ValueTask Read(CancellationToken cancellationToken)
    {
        var reader = Reader;
        switch((reader.NodeType, reader.Depth))
        {
            case (XmlNodeType.Element, 0) when reader.NamespaceURI == Xmpp.FramingNs:
                // Opening/closing stream element
                if(reader.LocalName == Xmpp.Open)
                {
                    var writer = Writer;
                    await writer.WriteStartElementAsync(null, Xmpp.Open.Value, Xmpp.FramingNs.Value);
                    await StreamsConnected(reader, writer);
                    await writer.WriteEndElementAsync();
                    await FlushCommand();

                    // Stream is ready
                    await EnterStream();
                }
                else if(reader.LocalName == Xmpp.Close)
                {
                    // Close
                    return;
                }
                else
                {
                    throw XmppStreamException.BadFormat();
                }
                break;

            case (XmlNodeType.Element, 0):
                // Individual command
                await EnterCommand(reader, cancellationToken);
                break;

            case (XmlNodeType.Element, _):
                await EnterPayload(reader, cancellationToken);
                break;

            case (XmlNodeType.EndElement, _):
                // End of payload or command
                await ExitPayload();
                break;
        }
    }

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
        await writer.WriteStartElementAsync(null, Xmpp.Close.Value, Xmpp.FramingNs.Value);
        await writer.WriteEndElementAsync();

        await writer.FlushAsync();
    }
}
