using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming message commands.
/// </summary>
internal class Message : BaseDelegatingMessageHandler<CapturingHandler<IMessageHandler>, EmptyDisposable, CommandContext>, IStanzaCommandHandler
{
    LocalizedString subject, body;
    string? nick, thread;
    ConversationState? state;

    public required override CommandContext Context { get => base.Context; init => base.Context = value; }

    protected sealed override CapturingHandler<IMessageHandler> InnerHandler { get; } = new();
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

    protected async sealed override ValueTask OnThread(string? identifier)
    {
        this.SetOnce(ref thread, identifier);
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

        return new MessageData
        {
            Subject = subject,
            Body = new(content.ToImmutable()),
            ThreadIdentifier = thread,
            Presentation = new(Nickname: nick),
            State = state ?? ConversationState.Unspecified,
            Extensions = new(InnerHandler.Calls.Count > 0 ? InnerHandler : null)
        };
    }

    protected virtual Event GetEvent()
    {
        return new MessageEvent
        {
            Origin = this.GetOrigin(),
            Type = Type.ToMessageType(),
            Processing = new()
            {
                Received = ConstructedTime,
                Accepted = WrittenTime,
                Published = DateTimeOffset.UtcNow
            },
            Data = GetMessage()
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
            await this.GetSession().Inbound(GetEvent());
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
            Origin = this.GetOrigin(),
            Processing = new()
            {
                Received = ConstructedTime,
                Accepted = time,
                Published = time
            },
            Data = new ErrorData(),
            OriginalData = GetMessage()
        };
    }
}
