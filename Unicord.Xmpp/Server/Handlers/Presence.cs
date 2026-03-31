using System;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming presence commands.
/// </summary>
internal class Presence : BaseDelegatingPresenceHandler<CapturingHandler<IPresenceHandler>, EmptyDisposable, ICommandContext>, IStanzaCommandHandler
{
    StatusType? show;
    LocalizedString statusText;
    string? nick;
    sbyte? priority;

    public required override ICommandContext Context {
#nullable disable
        get => base.Context; init => base.Context = value;
#nullable restore
    }

    protected sealed override CapturingHandler<IPresenceHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    protected DateTimeOffset ConstructedTime { get; }
    protected DateTimeOffset? WrittenTime { get; private set; }

    public Presence(in Stanza stanza)
    {
        ConstructedTime = DateTimeOffset.UtcNow;

        (Type, From, To) = this.OpenStanza(stanza);
    }

    protected async override ValueTask OnShow(Token<StatusType>? text)
    {
        this.SetOnce(ref show, text?.ToEnum());
        WrittenTime = DateTimeOffset.UtcNow;
    }

    protected async override ValueTask OnStatus(LanguageTaggedString? text)
    {
        statusText = statusText.Add(text, Context.Session.RemoteLanguage);
        WrittenTime = DateTimeOffset.UtcNow;
    }

    protected async override ValueTask OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
    }

    protected async override ValueTask OnPriority(sbyte? value)
    {
        this.SetOnce(ref priority, value);
    }

    protected async override ValueTask OnDelay(DateTimeOffset? stamp, XmppResource? from, LanguageTaggedString? reason)
    {
        // Ignore
    }

    /*protected async override ValueTask OnCapabilities(Token<CapabilitiesHash>? hash, string? node, string? version)
    {
        if(hash?.ToEnum() != CapabilitiesHash.Sha1)
        {
            return;
        }
        if(node == null || version == null)
        {
            return;
        }

        var nodeToken = Context.Session.GetToken<DiscoNode>(node + "#" + version);

        // Request capabilities
        await using var iq = await this.CreateRequest(async () => new CapabilitiesResultInfoQuery(nodeToken, version)
        {
            Context = Context with { Identifier = null }
        });
        await using var query = await iq.DiscoInfoQuery(nodeToken);
    }

    class CapabilitiesResultInfoQuery(Token<DiscoNode> nodeToken, string expectedHash) : InfoQueryHandler<CommandContext>
    {
        CapabilitiesParser<CommandContext>? handler;

        protected async override ValueTask<IDiscoInfoQueryHandler> OnDiscoInfoQuery(Token<DiscoNode>? node)
        {
            if(node != nodeToken)
            {
                // Wrong result
                return NullHandler.Instance;
            }

            return handler = new() { Context = Context };
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader)
        {
            return default;
        }

        public async override ValueTask DisposeAsync()
        {
            if(handler != null)
            {
                var capabilities = handler.Capabilities;
                using var sha1 = SHA1.Create();

                var computedHash = capabilities.ComputeHashCode(sha1, false);
                
                if(computedHash != expectedHash)
                {
                    // Try explicit language-only
                    computedHash = capabilities.ComputeHashCode(sha1, true);
                    if(computedHash != expectedHash)
                    {
                        // Fake or difference in algorithm
                        return;
                    }
                }

                // TODO Remember globally
            }
        }
    }*/

    protected virtual PresenceData GetPresence()
    {
        return new PresenceData {
            Status = new(
                show?.ToAvailability()
                ?? (Type == StanzaType.Unavailable ? Availability.Unavailable : Availability.Available),
                statusText
            ),
            Presentation = new(Nickname: nick),
            Priority = priority
        };
    }

    protected virtual Event GetEvent()
    {
        var origin = this.GetOrigin();
        var processing = new EventProcessing()
        {
            Received = ConstructedTime,
            Accepted = WrittenTime,
            Published = DateTimeOffset.UtcNow
        };
        var data = GetPresence();
        return Type switch {
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
            _ => throw XmppStanzaException.FeatureNotImplemented()
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
            await this.GetClientSession().Inbound(GetEvent());
        }
    }
}
