using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Server.Model.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming message commands.
/// </summary>
internal class Message : BaseDelegatingMessageHandler<CapturingHandler<IMessageHandler>, EmptyDisposable, CommandContext>, IStanzaCommandHandler
{
    LocalizedString subject, body;
    string? nick;
    ConversationState? state;

    public required override CommandContext Context { get => base.Context; init => base.Context = value; }

    protected sealed override CapturingHandler<IMessageHandler> InnerHandler { get; } = new CapturingHandler<IMessageHandler>();
    protected sealed override EmptyDisposable Disposable => default;

    public StanzaType? Type { get; }
    public XmppResource? From { get; }
    public XmppResource? To { get; }

    protected DateTimeOffset ConstructedTime { get; }
    protected DateTimeOffset? WrittenTime { get; private set; }

    public Message(in Stanza stanza)
    {
        ConstructedTime = DateTimeOffset.UtcNow;

        (Type, From, To) = this.OpenStanza(stanza);
    }

    protected async sealed override ValueTask OnBody(LanguageTaggedString? text)
    {
        WrittenTime = DateTimeOffset.UtcNow;
        body = body.Add(text, this.GetLanguage());
    }

    protected async sealed override ValueTask OnSubject(LanguageTaggedString? text)
    {
        subject = subject.Add(text, this.GetLanguage());
    }

    protected async sealed override ValueTask OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
    }

    protected async sealed override ValueTask OnDelay(DateTimeOffset? stamp, XmppResource? from, LanguageTaggedString? reason)
    {
        // TODO Preserve
    }

    protected async sealed override ValueTask OnActive()
    {
        this.SetOnce(ref state, ConversationState.Active);
    }

    protected async sealed override ValueTask OnInactive()
    {
        this.SetOnce(ref state, ConversationState.Inactive);
    }

    protected async sealed override ValueTask OnComposing()
    {
        this.SetOnce(ref state, ConversationState.Composing);
    }

    protected async sealed override ValueTask OnPaused()
    {
        this.SetOnce(ref state, ConversationState.Paused);
    }

    protected async sealed override ValueTask OnGone()
    {
        this.SetOnce(ref state, ConversationState.Gone);
    }

    protected virtual MessageData GetMessage()
    {
        var content = MessageBodyCollection.Empty.Data.ToBuilder();
        foreach(var body in this.body)
        {
            // TODO XHTML
            content[(MessageFormat.Plain, body.LanguageTag)] = body.Value;
        }

        var extensions = ImmutableDictionary<ExtensionType, object>.Empty;
        if(InnerHandler.Calls.Count > 0)
        {
            extensions = extensions.SetItem(ExtensionType.Xmpp, InnerHandler);
        }

        return new MessageData
        {
            Subject = subject,
            Body = new(content.ToImmutable()),
            Presentation = new(Nickname: nick),
            State = state ?? ConversationState.Unspecified,
            Extensions = extensions
        };
    }

    protected virtual Event GetEvent()
    {
        return new MessageEvent
        {
            From = this.GetSender()?.ToIdentifier(),
            To = this.GetRecipient()?.ToIdentifier(),
            TransactionIdentifier = this.GetIdentifier()?.ToIdentifier(),
            Type = Type.ToMessageType(),
            Received = ConstructedTime,
            Accepted = WrittenTime,
            Published = DateTimeOffset.UtcNow,
            Data = GetMessage()
        };
    }

    public sealed override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await Context.Server.Delivery.Post(GetEvent());
        }
    }
}

internal class ErrorMessage(in Stanza stanza) : Message(stanza)
{
    // TODO Error data

    protected override Event GetEvent()
    {
        var time = DateTimeOffset.UtcNow;
        return new ErrorEvent
        {
            From = this.GetSender()?.ToIdentifier(),
            To = this.GetRecipient()?.ToIdentifier(),
            TransactionIdentifier = this.GetIdentifier()?.ToIdentifier(),
            Received = ConstructedTime,
            Accepted = time,
            Published = time,
            Data = new ErrorData(),
            OriginalData = GetMessage()
        };
    }
}
