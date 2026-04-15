using System;
using System.Threading.Tasks;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming message commands.
/// </summary>
internal class Message : BaseDelegatingMessageHandler<CapturingHandler<IMessageHandler>, EmptyDisposable, ICommandContext>, ICommandHandler
{
    LocalizedString subject, body;
    string? nick, thread;
    ConversationState? state;

    protected sealed override CapturingHandler<IMessageHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    protected async sealed override ValueTask OnBody(LanguageTaggedString? text)
    {
        body = body.Add(text);
    }

    protected async sealed override ValueTask OnSubject(LanguageTaggedString? text)
    {
        subject = subject.Add(text);
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
            content[(MessageFormat.Plain, body.Language)] = body.Value;
        }

        return new MessageData
        {
            Subject = subject,
            Body = new(content.ToImmutable()),
            ThreadIdentifier = thread,
            Presentation = new(Nickname: nick),
            State = state ?? ConversationState.Unspecified,
            Extensions = InnerHandler.ToExtensions()
        };
    }

    protected virtual Event GetEvent()
    {
        return new MessageEvent
        {
            Origin = this.GetOrigin(),
            Type = (this.GetStanza().Type?.ToEnum()).ToMessageType(),
            Processing = this.GetProcessing(),
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
            this.Post(GetEvent());
        }
    }
}

internal class ErrorMessage : Message
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
        return new ErrorEvent
        {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = errorParser.GetError(GetMessage())
        };
    }
}
