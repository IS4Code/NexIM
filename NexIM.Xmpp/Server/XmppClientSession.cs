using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Tools;
using NexIM.Xmpp.Model;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Communication;
using NexIM.Xmpp.Server.Formats;
using NexIM.Xmpp.Server.Handlers;
using NexIM.Xmpp.Tools;

namespace NexIM.Xmpp.Server;

using CapabilitiesCache = FallbackCache<CapabilitiesIdentifier, XmppCapabilities>;

public class XmppClientSession : ClientSession
{
    readonly IXmppSession xmpp;

    readonly CapabilitiesCache capabilitiesCache = new();

    public XmppClientSession(Account account, string? resource, IXmppSession xmpp) : base(account, resource)
    {
        this.xmpp = xmpp;
    }

    protected async override ValueTask<StatusCode> Write(Event evnt)
    {
        switch(evnt)
        {
            case MessageEvent msgEvent:
            {
                await using var output = await xmpp.Message(msgEvent.ToStanza(xmpp));
                await WriteMessage(output, msgEvent.Data, evnt);
                return StatusCode.Received;
            }
            case PresenceEvent presEvent:
            {
                await using var output = await xmpp.Presence(presEvent.ToStanza(xmpp));
                await WritePresence(output, presEvent.Data, evnt);
                return StatusCode.Received;
            }
            case QueryEvent queryEvent:
            {
                await using var output = await xmpp.InfoQuery(queryEvent.ToStanza(xmpp));
                return await WriteInfoQuery(output, queryEvent.Data);
            }
            case ErrorEvent errorEvent:
            {
                return await WriteError(errorEvent.ToStanza(xmpp), errorEvent.Data, evnt);
            }
            default:
                return StatusCode.UnrecognizedRequest;
        }
    }

    private async ValueTask WriteMessage(IMessageHandler output, MessageData data, Event evnt)
    {
        // Basic elements

        await output.SubjectLocalizedNotNull(data.Subject);

        foreach(var ((format, language), body) in data.Body.Data)
        {
            if(format is MessageFormat.Plain)
            {
                // TODO Other formats
                await output.Body(new((string)body, language));
            }
        }

        if(data.ThreadIdentifier is { } thread)
        {
            await output.Thread(thread, data.ParentThreadIdentifier);
        }

        // Supported extensions

        await WriteSender(data.Presentation, output);

        switch(data.State)
        {
            case ConversationState.Active:
                await output.Active();
                break;
            case ConversationState.Inactive:
                await output.Inactive();
                break;
            case ConversationState.Composing:
                await output.Composing();
                break;
            case ConversationState.Paused:
                await output.Paused();
                break;
            case ConversationState.Gone:
                await output.Gone();
                break;
            default:
                break;
        }

        await WriteDelivery(output, data, evnt);

        // General extensions

        await WriteExtensions(output, data.Extensions);
    }

    private async ValueTask WritePresence(IPresenceHandler output, PresenceData data, Event evnt)
    {
        // Basic elements

        if(data.Status.Availability.ToStatusType() is { } statusType)
        {
            await output.Show(statusType.ToToken());
        }

        await output.StatusLocalizedNotNull(data.Status.Description);

        if(data.Priority is { } priority)
        {
            await output.Priority(priority);
        }

        // Supported extensions

        // TODO CancellationToken
        if(await data.Capabilities.Get(static c => c.Identifier) is { } identifier)
        {
            await output.Capabilities(xmpp.GetToken<CapabilitiesHash>(identifier.VersionType), identifier.Application, identifier.Version, null);
        }

        await WriteSender(data.Presentation, output);

        await WriteDelivery(output, data, evnt);

        // General extensions

        await WriteExtensions(output, data.Extensions);
    }

    private async ValueTask WriteDelivery(IDeliveryHandler output, DeliveryData data, Event evnt)
    {
        if(data.Timing is { } timing && evnt.Created != evnt.Published)
        {
            // Delayed by sender
            await output.Delay(evnt.Created.UtcDateTime, timing.ObservedBy?.ToResource(xmpp), timing.Description);
        }
        else if(DateTimeOffset.UtcNow - evnt.Created > Configuration.XmppMinDelayTime)
        {
            // Delayed by receiver
            await output.Delay(evnt.Created.UtcDateTime, xmpp.LocalResource, null);
        }

        if(data.MessageRelations is { } messageRelations)
        {
            if(output is IMessageHandler messageHandler)
            {
                foreach(var (relation, text) in messageRelations)
                {
                    switch(relation.Type)
                    {
                        case DeliveryRelationType.Reply:
                            await messageHandler.ReplyTo(relation.MessageIdentifier, relation.Originator?.ToResource(xmpp));
                            break;

                        case DeliveryRelationType.Refer:
                            await messageHandler.Reference(relation.MessageIdentifier, relation.Originator?.ToResource(xmpp));
                            break;

                        // TODO Check originator
                        case DeliveryRelationType.DispositionNotify:
                            await messageHandler.ReceiptResponse(relation.MessageIdentifier);
                            break;

                        case DeliveryRelationType.DisplayNotify:
                            await messageHandler.DisplayedResponseLocalized(relation.MessageIdentifier, text);
                            break;
                    }
                }
            }
        }

        if(data.AddressRelations is { } addressRelations)
        {
            if(output is IMessageHandler messageHandler)
            {
                foreach(var entry in addressRelations)
                {
                    switch(entry.Key.Type)
                    {
                        // TODO Notification to a different identifier
                        case DeliveryRelationType.DispositionNotify:
                            await messageHandler.ReceiptRequest();
                            break;
                        case DeliveryRelationType.DisplayNotify:
                            await messageHandler.DisplayedRequestLocalized(entry.Value);
                            break;
                        case DeliveryRelationType.NoStore:
                            await messageHandler.NoStore();
                            break;
                        case DeliveryRelationType.NoCopy:
                            await messageHandler.NoCopy();
                            break;
                        case DeliveryRelationType.NoPermanentStore:
                            await messageHandler.NoPermanentStore();
                            break;
                        case DeliveryRelationType.Store:
                            await messageHandler.Store();
                            break;
                    }
                }
            }

            var remoteAddress = xmpp.RemoteResource;
            IAddressesHandler? addressesHandler = null;
            try
            {
                foreach(var entry in addressRelations)
                {
                    // Delivering only those that are the direct recipients of this event
                    bool delivered = entry.Key.Recipient is { } recipient && !evnt.To.Contains(recipient);

                    // Will be initialized if required
                    addressesHandler = await AddressesFormatter.WriteTo(entry, output, addressesHandler, remoteAddress, delivered);
                }
            }
            finally
            {
                await addressesHandler.DisposeNotNullAsync();
            }
        }
    }

    private async ValueTask<StatusCode> WriteInfoQuery(IInfoQueryHandler output, QueryData? queryData)
    {
        switch(queryData)
        {
            case GeneralQueryData:
                await WriteExtensions(output, queryData.Extensions);
                return StatusCode.Received;

            case RosterQueryData data:
                await using(var handler = await output.RosterQuery(version: data.Tag))
                {
                    switch(data)
                    {
                        case RosterUpdateData updateData:
                            await RosterFormatter.WriteTo(updateData.Contact, handler, false);
                            break;
                        case RosterRemoveData removeData:
                            await RosterFormatter.WriteTo(removeData.Contact, handler, true);
                            break;
                        default:
                            foreach(var contact in data.Roster ?? Enumerable.Empty<Contact>())
                            {
                                await RosterFormatter.WriteTo(contact, handler, false);
                            }
                            break;
                    }
                    await WriteExtensions(handler, data.Extensions);
                }
                return StatusCode.Received;

            case PrivateData data:
                await using(var handler = await output.PrivateQuery())
                {
                    await WriteExtensions(handler, data.Extensions);
                }
                return StatusCode.Received;

            case VCardQueryData data:
                await using(var handler = await output.VCard())
                {
                    if(data.VCard is { } vcard)
                    {
                        await VCardFormatter.WriteTo(vcard, handler);
                    }
                    await WriteExtensions(handler, data.Extensions);
                }
                return StatusCode.Received;

            case TimeData data:
                await using(var handler = await output.Time())
                {
                    if(data.DateTime is { } dateTime)
                    {
                        await TimeFormatter.WriteTo(dateTime, handler);
                    }
                    await WriteExtensions(handler, data.Extensions);
                }
                return StatusCode.Received;

            case PingData:
                await output.Ping();
                return StatusCode.Received;

            default:
                return StatusCode.UnrecognizedRequest;
        }
    }

    private async ValueTask WriteExtensions<THandler>(THandler handler, EventExtensions extensions) where THandler : IPayloadHandler
    {
        foreach(var extension in extensions)
        {
            if(extension is ICapturingHandler<THandler> capture)
            {
                await capture.Replay(handler);
            }
        }
    }

    private async ValueTask<StatusCode> WriteError(Stanza stanza, ErrorData data, Event evnt)
    {
        if(data.ErrorCode.ToStanzaException() is not { } exception)
        {
            return StatusCode.InvalidParameter;
        }

        switch(data.OriginalData)
        {
            case MessageData msgData:
            {
                await using var output = await xmpp.Message(stanza);
                await WriteMessage(output, msgData, evnt);
                await WriteErrorData(output);
                return StatusCode.Received;
            }
            case PresenceData presData:
            {
                await using var output = await xmpp.Presence(stanza);
                await WritePresence(output, presData, evnt);
                await WriteErrorData(output);
                return StatusCode.Received;
            }
            case QueryData queryData:
            {
                await using var output = await xmpp.InfoQuery(stanza);
                try
                {
                    return await WriteInfoQuery(output, queryData);
                }
                finally
                {
                    await WriteErrorData(output);
                }
            }
            default:
                return StatusCode.UnrecognizedRequest;
        }

        async ValueTask WriteErrorData(IStanzaHandler output)
        {
            await using var error = await output.Error(data.RecommendedAction.ToErrorType().ToToken(), (int?)data.HttpStatusCode, data.Reporter?.ToResource(xmpp));

            // Basic elements

            await exception.Output(error);

            await error.TextLocalizedNotNull(data.Description);

            // General extensions

            await WriteExtensions(error, data.Extensions);
        }
    }

    async ValueTask WriteSender(Presentation sender, IPresentationHandler presence)
    {
        if(sender.Nickname is { } nick)
        {
            await presence.Nickname(nick);
        }
    }

    internal Remote<XmppCapabilities> GetCapabilities(Token<CapabilitiesHash> hash, string node, string version)
    {
        var identifier = new CapabilitiesIdentifier(node, version, hash.Value);
        var task = capabilitiesCache.Get(identifier, async () => {
            // Not used locally yet
            var tcs = new TaskCompletionSource<XmppCapabilities>();

            if(!CapabilitiesParser<ICommandContext>.IsSupportedHashAlgorithm(hash))
            {
                // No way to verify - request only locally and wait for result
                await RequestCapabilities(hash, node, version, tcs);
                return await tcs.Task;
            }

            // May be unverified regardless, but we still want to retrieve the local result

            return await await Task.WhenAny(tcs.Task, CapabilitiesCache.Global.Get(identifier, async () => {
                await RequestCapabilities(hash, node, version, tcs);
                var result = await tcs.Task;
                if(result is XmppCapabilities { Verified: false })
                {
                    // Must not be stored globally
                    return null;
                }
                return result;
            }));
        });

        return new(new CapabilitiesProvider(identifier, task));
    }

    private async ValueTask RequestCapabilities(Token<CapabilitiesHash> hash, string node, string version, TaskCompletionSource<XmppCapabilities> tcs)
    {
        // Atomize full node to verify quickly
        var nodeToken = xmpp.GetToken<DiscoNode>(node + "#" + version);

        var identifier = xmpp.NewStanzaIdentifier();
        xmpp.RegisterCallback(identifier, () => new(new CapabilitiesResultInfoQuery(nodeToken, hash, version, tcs) {
            Context = (ICommandContext)xmpp
        }));

        await using var iq = await xmpp.InfoQuery(new(Type: StanzaType.Get.ToToken(), From: xmpp.LocalResource, Identifier: identifier));
        await using var query = await iq.DiscoInfoQuery(nodeToken);
    }

    class CapabilitiesResultInfoQuery(Token<DiscoNode> nodeToken, Token<CapabilitiesHash> hashAlgorithm, string expectedHash, TaskCompletionSource<XmppCapabilities> tcs) : InfoQueryHandler<ICommandContext>
    {
        CapabilitiesParser<ICommandContext>? handler;

        protected async override ValueTask<IDiscoInfoQueryHandler> OnDiscoInfoQuery(Token<DiscoNode>? node)
        {
            if(node != nodeToken)
            {
                // Wrong result
                return NullHandler.Instance;
            }

            return this.SetOnce(ref handler, new() { Context = Context });
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader)
        {
            return default;
        }

        public async override ValueTask DisposeAsync()
        {
            if(handler == null)
            {
                // TODO Ask again?
                return;
            }

            // Store result
            tcs.TrySetResult(handler.GetCapabilities(nodeToken, hashAlgorithm, expectedHash));
        }
    }
}
