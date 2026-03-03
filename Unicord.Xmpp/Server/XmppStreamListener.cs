using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary.Standard;

public abstract class XmppStreamListener<TContext> : XmppXmlListener<XmppStreamSession>
{
    protected override bool PrettyOutput => true;

    public XmppStreamListener(IXmppReceiver<XmppStreamSession> receiver) : base(receiver, ConformanceLevel.Document)
    {

    }

    protected abstract ValueTask<XmppStreamSession> StartSession(TContext context, CancellationToken cancellationToken);

    protected async ValueTask HandleStream(TContext context, CancellationToken cancellationToken)
    {
        // Initialize outgoing session
        await using var session = await StartSession(context, cancellationToken);

        await HandleSession(session, cancellationToken);
    }

    protected async override ValueTask Read(XmppStreamSession session, CancellationToken cancellationToken)
    {
        var reader = session.Reader;
        var handler = session.MainHandler;
        int depth = reader.Depth;
        switch(reader.NodeType)
        {
            case XmlNodeType.XmlDeclaration:
                return;

            case XmlNodeType.Element:
                const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;
                bool isEmpty = reader.IsEmptyElement;
                switch(depth)
                {
                    case 0:
                        // Root stream element
                        if(reader.NamespaceURI != StreamsNs)
                        {
                            throw XmppStreamException.BadNamespacePrefix();
                        }
                        if(reader.LocalName != Stream)
                        {
                            throw XmppStreamException.BadFormat();
                        }

                        var writer = session.Writer;

                        await writer.WriteStartElementAsync(Stream.Value, Stream.Value, StreamsNs.Value);
                        await writer.WriteAttributeStringAsync(Xmlns.Value, Stream.Value, XmlnsNs.Value, StreamsNs.Value);
                        await writer.WriteAttributeStringAsync(null, Xmlns.Value, null, JabberClientNs.Value);

                        await StreamStarted(reader, writer, session);

                        // Stream is ready
                        await handler.StreamStarted();
                        break;

                    case 1:
                        // Individual command
                        if(await EnterCommand(session, reader, handler) is (true, var commandHandler))
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
                        break;

                    default:
                        // Payload of a known command
                        if(await Decoder.DecodePayload(reader, session.Handlers.Get<IPayloadHandler>()) is (true, var payloadHandler))
                        {
                            // Recognized payload type
                            await EnterHandler(payloadHandler);
                        }
                        else
                        {
                            // Unknown element
                            using var subtreeReader = reader.ReadSubtree();
                            var element = await XElement.LoadAsync(subtreeReader, elementLoadOptions, cancellationToken);
                            await session.Handlers.Get<IPayloadHandler>().Other(element);
                        }
                        break;
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
                            session.Handlers.Push(handler);
                        }
                    }
                    return default;
                }
                break;

            case XmlNodeType.EndElement:
                switch(depth)
                {
                    case 0:
                        // End of stream
                        return;
                    default:
                        if(!session.Handlers.TryPop(out var top))
                        {
                            return;
                        }
                        await top.DisposeAsync();
                        break;
                }
                break;
        }
    }
}
