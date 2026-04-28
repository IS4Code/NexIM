using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Formats;

namespace NexIM.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming presence commands.
/// </summary>
internal class Presence : BaseDelegatingPresenceHandler<CapturingHandler<IPresenceHandler>, EmptyDisposable, ICommandContext>, ICommandHandler
{
    StatusType? show;
    LocalizedString.Builder statusTextBuilder;
    string? nick;
    sbyte? priority;
    CapabilitiesHandle? caps;
    (DateTime? timestamp, XmppResource? from, LanguageTaggedString? reason)? delay;
    AddressesParser<ICommandContext>? addressesParser;

    protected sealed override CapturingHandler<IPresenceHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    protected async override ValueTask OnShow(Token<StatusType>? text)
    {
        this.SetOnce(ref show, text?.ToEnum());
    }

    protected async override ValueTask OnStatus(LanguageTaggedString? text)
    {
        statusTextBuilder.Add(text);
    }

    protected async override ValueTask OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
    }

    protected async override ValueTask OnPriority(sbyte? value)
    {
        this.SetOnce(ref priority, value);
    }

    protected async override ValueTask OnDelay(DateTime? timestamp, XmppResource? from, LanguageTaggedString? reason)
    {
        if(!this.VerifyOwnership(from))
        {
            return;
        }
        this.SetOnce(ref delay, (timestamp, from, reason));
    }

    protected async override ValueTask<IAddressesHandler> OnAddresses()
    {
        return this.SetOnce(ref addressesParser, this.GetHandler<AddressesParser<ICommandContext>>());
    }

    protected async override ValueTask OnCapabilities(Token<CapabilitiesHash>? hash, string? node, string? version, string? extension)
    {
        if(node is null || version is null || hash is not { } hashValue || extension is not null)
        {
            // Unrecognized - store as extension
            await base.OnCapabilities(hash, node, version, extension);
            return;
        }

        this.SetOnce(ref caps, this.GetClientSession().GetCapabilities(hashValue, node, version));
    }

    protected virtual PresenceData GetPresence()
    {
        return new PresenceData {
            Status = new(
                show?.ToAvailability()
                ?? (this.GetStanza().Type?.ToEnum() == StanzaType.Unavailable ? Availability.Unavailable : Availability.Available),
                statusTextBuilder.TryToString()
            ),
            Presentation = new(Nickname: nick),
            Priority = priority,
            Capabilities = caps,
            DelayedBy = delay?.from?.ToIdentifier(),
            DelayReason = delay?.reason,
            Addresses = addressesParser?.Addresses,
            ReceiptIdentifier = null,
            Extensions = InnerHandler.ToExtensions()
        }.Deduplicate();
    }

    protected virtual Event GetEvent()
    {
        var origin = this.GetOrigin(addressesParser?.Recipients);
        var processing = this.GetProcessing(delay?.timestamp);
        var data = GetPresence();
        return this.GetStanza().Type?.ToEnum() switch {
            null or StanzaType.Unavailable => new StatusUpdateEvent {
                Origin = origin,
                Processing = processing,
                Data = data
            },
            StanzaType.Probe => new StatusRequestEvent {
                Origin = origin,
                Processing = processing,
                Data = data
            },
            StanzaType.Subscribe => new SubscriptionRequestedEvent {
                Origin = origin,
                Processing = processing,
                Data = data
            },
            StanzaType.Subscribed => new SubscriptionAcceptedEvent {
                Origin = origin,
                Processing = processing,
                Data = data
            },
            StanzaType.Unsubscribed => new SubscriptionRejectedEvent {
                Origin = origin,
                Processing = processing,
                Data = data
            },
            StanzaType.Unsubscribe => new SubscriptionCancelledEvent {
                Origin = origin,
                Processing = processing,
                Data = data
            },
            _ => throw XmppStanzaException.BadRequest()
        };
    }

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal class ErrorPresence : Presence
{
    ErrorParser? errorParser;

    protected async sealed override ValueTask<IStanzaErrorHandler> OnError(Token<ErrorType>? type, int? code, XmppResource? by)
    {
        return this.SetOnce(ref errorParser, new(type, code, by) { Context = Context });
    }

    protected override Event GetEvent()
    {
        if(errorParser == null)
        {
            throw XmppStanzaException.BadRequest();
        }
        return new ErrorEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = errorParser.GetError(GetPresence())
        };
    }
}
