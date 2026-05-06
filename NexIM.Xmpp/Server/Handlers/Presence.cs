using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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
    Remote<Capabilities>? caps;
    Remote<TemporaryFile>? photo;
    (DateTime? timestamp, DeliveryTiming timing)? delay;
    AddressesParser<ICommandContext>? _addressesParser;
    AddressesParser<ICommandContext> addressesParser => _addressesParser ??= this.GetHandler<AddressesParser<ICommandContext>>();

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
        this.SetOnce(ref delay, (timestamp, new(from?.ToIdentifier(this.GetSession()), reason)));
    }

    protected async override ValueTask<IAddressesHandler> OnAddresses()
    {
        return addressesParser;
    }

    protected async override ValueTask OnCapabilities(Token<CapabilitiesHash>? hash, string? node, string? version, string? extension)
    {
        if(node is null || version is null || hash is not { } hashValue || extension is not null)
        {
            // Unrecognized - store as extension
            await base.OnCapabilities(hash, node, version, extension);
            return;
        }

        this.SetOnce(ref caps, this.GetClientSession().GetCapabilities(hashValue, node, version).TryCast<Capabilities>());
    }

    protected async override ValueTask<IVCardUpdateHandler> OnVCardUpdate()
    {
        var handler = this.GetHandler<VCardUpdate>();
        handler.parent = this;
        return handler;
    }

    protected virtual PresenceData GetPresence()
    {
        return new PresenceData {
            Status = new(
                show?.ToAvailability()
                ?? (this.GetStanza().Type?.ToEnum() == StanzaType.Unavailable ? Availability.Unavailable : Availability.Available),
                statusTextBuilder.TryToString()
            ),
            Presentation = new() {
                Nickname = nick,
                Avatar = photo
            },
            Priority = priority,
            Capabilities = caps ?? default,
            Timing = delay?.timing,
            AddressRelations = _addressesParser?.Addresses,
            MessageRelations = null,
            Extensions = InnerHandler.ToExtensions()
        }.Deduplicate();
    }

    protected virtual Event GetEvent()
    {
        var origin = this.GetOrigin(_addressesParser?.Recipients);
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

    sealed class VCardUpdate : BaseVCardUpdateHandler<ICommandContext>
    {
        public Presence parent = null!;

        protected async override ValueTask OnPhoto(Hex<ArraySegment<byte>>? hash)
        {
            this.SetOnce(ref parent.photo, new(new FileProvider(hash?.Value ?? default, this.GetServer())));
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => default;
        public override ValueTask DisposeAsync() => default;
    }

    sealed class FileProvider(ArraySegment<byte> hash, NexIM.Server.Server server) : IdentifierRemoteProvider<TemporaryFile, UploadedFile, ArraySegment<byte>>
    {
        protected override ArraySegment<byte> Identifier => hash;
        protected override MemberInfo IdentifierMember => identifierMember;

        protected override ValueTask<UploadedFile?> Load(CancellationToken cancellationToken)
        {
            return server.FindUploadedFileBySha1(hash, cancellationToken);
        }

        static readonly MemberInfo identifierMember =
            ((MemberExpression)((Expression<Func<UploadedFile, ArraySegment<byte>>>)(x => x.Sha1Hash)).Body).Member;

        public bool Equals(FileProvider other)
        {
            return hash.AsSpan().SequenceEqual(other.Identifier.AsSpan());
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.AddBytes(hash.AsSpan());
            return hashCode.ToHashCode();
        }

        public override bool Equals(IRemoteProvider obj) => obj is FileProvider other && Equals(other);
        public override bool References(UploadedFile? other) => other != null && other.Sha1Hash.AsSpan().SequenceEqual(hash.AsSpan());
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
