using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using Xmpp = XmppVocabulary.Standard;

public abstract class XmppFrameSession(Stream stream) : XmppAuthSession(stream)
{
    protected async override ValueTask Read(CancellationToken cancellationToken)
    {
        var reader = Reader;
        var handler = MainHandler;
        switch(reader.NodeType)
        {
            case XmlNodeType.Element:
                const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;
                bool isEmpty = reader.IsEmptyElement;
                if(reader.Depth == 0)
                {
                    if(reader.NamespaceURI == Xmpp.FramingNs)
                    {
                        // Opening/closing stream element
                        if(reader.LocalName == Xmpp.Open)
                        {
                            var writer = Writer;

                            await writer.WriteStartElementAsync(null, Xmpp.Open.Value, Xmpp.FramingNs.Value);

                            await StreamStarted(reader);

                            await writer.WriteEndElementAsync();

                            await FlushCommand();

                            // Stream is ready
                            await handler.StreamStarted();
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
                    }
                    else
                    {
                        // Individual command
                        if(await EnterCommand(reader, handler) is (true, var commandHandler))
                        {
                            // Recognized command type
                            await EnterHandler(commandHandler);
                        }
                        else
                        {
                            // Unknown type
                            using var subtreeReader = reader.ReadSubtree();
                            var element = await XElement.LoadAsync(subtreeReader, elementLoadOptions, cancellationToken);
                            await handler.Other(element);
                        }
                    }
                }
                else
                {
                    // Payload of a known command
                    if(await Decoder.DecodePayload(reader, Handlers.Get<IPayloadHandler>()) is (true, var payloadHandler))
                    {
                        // Recognized payload type
                        await EnterHandler(payloadHandler);
                    }
                    else
                    {
                        // Unknown element
                        using var subtreeReader = reader.ReadSubtree();
                        var element = await XElement.LoadAsync(subtreeReader, elementLoadOptions, cancellationToken);
                        await Handlers.Get<IPayloadHandler>().Other(element);
                    }
                }

                ValueTask EnterHandler(IPayloadHandler? handler)
                {
                    if(handler != null)
                    {
                        if(isEmpty)
                        {
                            // No EndElement, close now
                            return handler.DisposeAsync();
                        }
                        else
                        {
                            Handlers.Push(handler);
                        }
                    }
                    return default;
                }
                break;

            case XmlNodeType.EndElement:
                if(!Handlers.TryPop(out var top))
                {
                    return;
                }
                await top.DisposeAsync();
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
        await writer.WriteStartElementAsync(null, XmppVocabulary.Standard.Close.Value, XmppVocabulary.Standard.FramingNs.Value);
        await writer.WriteEndElementAsync();

        await writer.FlushAsync();
    }
}
