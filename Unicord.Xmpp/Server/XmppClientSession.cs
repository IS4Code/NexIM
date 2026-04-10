using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Server;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Communication;
using Unicord.Xmpp.Server.Handlers;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Server;

using CapabilitiesCache = FallbackCache<CapabilitiesIdentifier, ICapabilities>;

public class XmppClientSession : ClientSession
{
    readonly IXmppSession xmpp;

    bool receivesRosterUpdates;
    ICollection<Contact>? lastUpdatedRoster;

    readonly CapabilitiesCache capabilitiesCache = new();

    public XmppClientSession(Account account, string? resource, IXmppSession xmpp) : base(account, resource)
    {
        this.xmpp = xmpp;
    }

    public void SubscribeToRosterUpdates()
    {
        receivesRosterUpdates = true;
    }

    protected async override ValueTask<ErrorCode> Write(Event evnt)
    {
        switch(evnt)
        {
            case MessageEvent msgEvent:
            {
                await using var output = await xmpp.Message(msgEvent.ToStanza(xmpp));
                await WriteMessage(output, msgEvent.Data);
                return ErrorCode.Success;
            }
            case PresenceEvent presEvent:
            {
                await using var output = await xmpp.Presence(presEvent.ToStanza(xmpp));
                await WritePresence(output, presEvent.Data);
                return ErrorCode.Success;
            }
            case QueryEvent queryEvent:
            {
                await using var output = await xmpp.InfoQuery(queryEvent.ToStanza(xmpp));
                return await WriteInfoQuery(output, queryEvent.Data);
            }
            case ErrorEvent errorEvent:
            {
                return await WriteError(errorEvent.ToStanza(xmpp), errorEvent.Data);
            }
        }
        return ErrorCode.Unrecognized;
    }

    private async ValueTask WriteMessage(IMessageHandler output, MessageData? data)
    {
        if(data is null)
        {
            return;
        }

        // Basic elements

        foreach(var subject in data.Subject)
        {
            await output.Subject(subject);
        }

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
            await output.Thread(thread);
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

        // General extensions

        foreach(var extension in data.Extensions)
        {
            if(extension is CapturingHandler<IMessageHandler> capture)
            {
                await capture.Replay(output);
            }
        }
    }

    private async ValueTask WritePresence(IPresenceHandler output, PresenceData? data)
    {
        if(data is null)
        {
            return;
        }

        // Basic elements

        if(data.Status.Availability.ToStatusType() is { } statusType)
        {
            await output.Show(statusType.ToToken());
        }

        foreach(var description in data.Status.Description)
        {
            await output.Status(description);
        }

        if(data.Priority is { } priority)
        {
            await output.Priority(priority);
        }

        // Supported extensions

        await WriteSender(data.Presentation, output);

        // General extensions

        foreach(var extension in data.Extensions)
        {
            if(extension is CapturingHandler<IPresenceHandler> capture)
            {
                await capture.Replay(output);
            }
        }
    }

    private async ValueTask<ErrorCode> WriteInfoQuery(IInfoQueryHandler output, QueryData? data)
    {
        switch(data)
        {
            case GeneralQueryData:
                foreach(var extension in data.Extensions)
                {
                    if(extension is CapturingHandler<IInfoQueryHandler> capture)
                    {
                        await capture.Replay(output);
                    }
                }
                return ErrorCode.Success;
        }
        return ErrorCode.Unrecognized;
    }

    private async ValueTask<ErrorCode> WriteError(Stanza stanza, ErrorData? data)
    {
        switch(data?.OriginalData)
        {
            case MessageData msgData:
            {
                await using var output = await xmpp.Message(stanza);
                await WriteMessage(output, msgData);
                await WriteErrorData(output);
                return ErrorCode.Success;
            }
            case PresenceData presData:
            {
                await using var output = await xmpp.Presence(stanza);
                await WritePresence(output, presData);
                await WriteErrorData(output);
                return ErrorCode.Success;
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
                return ErrorCode.Unrecognized;
        }

        async ValueTask WriteErrorData(IStanzaHandler output)
        {
            await using var error = await output.Error(data.RecommendedAction.ToErrorType().ToToken(), (int?)data.StatusCode, data.Reporter?.ToResource());

            // Basic elements

            await data.ErrorCode.ToStanzaException().Output(error);

            foreach(var text in data.Description)
            {
                await error.Text(text);
            }

            // General extensions

            foreach(var extension in data.Extensions)
            {
                if(extension is CapturingHandler<IStanzaErrorHandler> capture)
                {
                    await capture.Replay(error);
                }
            }
        }
    }

    async ValueTask WriteSender(SenderPresentation sender, IPresentationHandler presence)
    {
        if(sender.Nickname is { } nick)
        {
            await presence.Nickname(nick);
        }
    }

    internal CapabilitiesHandle GetCapabilities(Token<CapabilitiesHash> hash, string node, string version)
    {
        var identifier = new CapabilitiesIdentifier(node, version, hash.Value);
        var task = capabilitiesCache.Get(identifier, async () => {
            // Not used locally yet
            var tcs = new TaskCompletionSource<ICapabilities>();

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
                if(result is Capabilities { Verified: false })
                {
                    // Must not be stored globally
                    return null;
                }
                return result;
            }));
        });

        return new(identifier, Cached<ICapabilities>.FromTask(task));
    }

    private async ValueTask RequestCapabilities(Token<CapabilitiesHash> hash, string node, string version, TaskCompletionSource<ICapabilities> tcs)
    {
        // Atomize full node to verify quickly
        var nodeToken = xmpp.GetToken<DiscoNode>(node + "#" + version);

        var identifier = xmpp.NewStanzaIdentifier();
        xmpp.RegisterCallback(identifier, () => new(new CapabilitiesResultInfoQuery(nodeToken, hash, version, tcs)
        {
            Context = (ICommandContext)xmpp
        }));

        await using var iq = await xmpp.InfoQuery(new(Type: StanzaType.Get.ToToken(), From: xmpp.LocalResource, Identifier: identifier));
        await using var query = await iq.DiscoInfoQuery(nodeToken);
    }

    class CapabilitiesResultInfoQuery(Token<DiscoNode> nodeToken, Token<CapabilitiesHash> hashAlgorithm, string expectedHash, TaskCompletionSource<ICapabilities> tcs) : InfoQueryHandler<ICommandContext>
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
            tcs.TrySetResult(handler.GetCapabilities(hashAlgorithm, expectedHash));
        }
    }

    public static string GetContactsVersion(ICollection<Contact> contacts)
    {
        // New immutable instance each time
        return unchecked((uint)contacts.GetHashCode()).ToString("x08");
    }

    bool CheckContactUpdate(Contact contact, ICollection<Contact> current)
    {
        if(!receivesRosterUpdates)
        {
            // Not interested
            return false;
        }

        if(current == lastUpdatedRoster)
        {
            // No update
            return false;
        }

        lastUpdatedRoster = current;

        if(!contact.SubscriptionState.ApprovedTo)
        {
            // Not explicitly added
            return false;
        }

        return true;
    }

    protected async override ValueTask<ErrorCode> ContactUpdated(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return ErrorCode.Success;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: StanzaType.Set.ToToken()));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await SendContact(roster, contact);
        return ErrorCode.Success;
    }

    protected async override ValueTask<ErrorCode> ContactRemoved(Contact contact, ICollection<Contact> current)
    {
        if(!CheckContactUpdate(contact, current))
        {
            return ErrorCode.Success;
        }

        await using var iq = await xmpp.InfoQuery(new Stanza(From: xmpp.RemoteResource?.Bare, To: xmpp.RemoteResource, Type: StanzaType.Set.ToToken()));

        await using var roster = await iq.RosterQuery(GetContactsVersion(current));
        await using var item = await roster.Item(contact.Account.ToResource(null), contact.Name, RosterSubscriptionDirection.Remove.ToToken(), null, null);
        return ErrorCode.Success;
    }

    public static async ValueTask SendContact(IRosterQueryHandler roster, Contact contact)
    {
        if(!contact.SubscriptionState.ApprovedTo)
        {
            // Invisible
            return;
        }

        await using var item = await roster.Item(contact.Account.ToResource(null), contact.Name, contact.SubscriptionState.Direction switch
        {
            SubscriptionDirection.To => RosterSubscriptionDirection.To.ToToken(),
            SubscriptionDirection.From => RosterSubscriptionDirection.From.ToToken(),
            SubscriptionDirection.Both => RosterSubscriptionDirection.Both.ToToken(),
            _ => RosterSubscriptionDirection.None.ToToken()
        }, contact.SubscriptionState.PendingTo ? RosterPendingAction.Subscription.ToToken() : null, contact.SubscriptionState.ApprovedFrom);

        if(contact.Group is { } group)
        {
            await item.Group(group);
        }
    }
}
