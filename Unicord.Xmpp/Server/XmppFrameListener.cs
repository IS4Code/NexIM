using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Grammar;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

using static XmppVocabulary.Standard;

public abstract class XmppFrameListener<TContext> : XmppXmlListener<XmppFrameSession>
{
    public XmppFrameListener(IXmppReceiver<XmppFrameSession> receiver) : base(receiver, ConformanceLevel.Fragment)
    {

    }

    protected abstract ValueTask<XmppFrameSession> StartSession(TContext context, CancellationToken cancellationToken);

    protected async ValueTask HandleSocket(TContext context, CancellationToken cancellationToken)
    {
        // Initialize outgoing session
        await using var session = await StartSession(context, cancellationToken);

        await HandleSession(session, cancellationToken);
    }

    protected async override ValueTask Read(XmppFrameSession session, CancellationToken cancellationToken)
    {
        var reader = session.Reader;
        var handler = session.MainHandler;
        switch(reader.NodeType)
        {
            case XmlNodeType.Element:
                const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;
                bool isEmpty = reader.IsEmptyElement;
                if(reader.Depth == 0)
                {
                    if(reader.NamespaceURI == FramingNs)
                    {
                        // Opening/closing stream element
                        if(reader.LocalName == Open)
                        {
                            var writer = session.Writer;

                            await writer.WriteStartElementAsync(null, Open.Value, FramingNs.Value);

                            await StreamStarted(reader, writer, session);

                            await writer.WriteEndElementAsync();

                            await session.FlushCommand();

                            // Stream is ready
                            await handler.StreamStarted();
                        }
                        else if(reader.LocalName == Close)
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
                    }
                }
                else
                {
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
                if(!session.Handlers.TryPop(out var top))
                {
                    return;
                }
                await top.DisposeAsync();
                break;
        }
    }
}
