using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Formats;

namespace NexIM.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming message commands.
/// </summary>
internal class Message : BaseDelegatingMessageHandler<CapturingHandler<IMessageHandler>, EmptyDisposable, ICommandContext>, ICommandHandler
{
    LocalizedString subject, body;
    string? nick, receiptFor;
    (string? identifier, string? parent)? thread;
    ConversationState? state;
    (DateTime? timestamp, XmppResource? from, LanguageTaggedString? reason)? delay;
    AddressesParser<ICommandContext>? addressesParser;

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

    protected async sealed override ValueTask OnThread(string? identifier, string? parent)
    {
        this.SetOnce(ref thread, (parent, identifier));
    }

    protected async sealed override ValueTask OnNickname(string? text)
    {
        this.SetOnce(ref nick, text);
    }

    protected async sealed override ValueTask OnDelay(DateTime? timestamp, XmppResource? from, LanguageTaggedString? reason)
    {
        if(!this.VerifyOwnership(from))
        {
            return;
        }
        this.SetOnce(ref delay, (timestamp, from, reason));
    }

    protected async override ValueTask<IAddressesHandler> OnAddresses()
    {
        return addressesParser ??= this.GetHandler<AddressesParser<ICommandContext>>();
    }

    protected async override ValueTask OnReceiptRequest()
    {
        var parser = addressesParser ??= this.GetHandler<AddressesParser<ICommandContext>>();
        parser.Add(DeliveryAddress.DispositionNotification);
    }

    protected async override ValueTask OnReceiptResponse(string? id)
    {
        this.SetOnce(ref receiptFor, id);
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
        var addresses = addressesParser?.Addresses;
        if(receiptFor != null && addresses.HasValue)
        {
            if(addresses.GetValueOrDefault().Contains(DeliveryAddress.DispositionNotification))
            {
                throw XmppStanzaException.BadRequest("A receipt cannot be requested for a message acknowledgment.");
            }
        }

        var content = MessageBodyCollection.Empty.Data.ToBuilder();
        foreach(var body in this.body)
        {
            // TODO XHTML
            content[(MessageFormat.Plain, body.Language)] = body.Value;
        }

        return new MessageData {
            Subject = subject,
            Body = new(content.ToImmutable()),
            ThreadIdentifier = thread?.identifier,
            ParentThreadIdentifier = thread?.parent,
            Presentation = new(Nickname: nick),
            State = state ?? ConversationState.Unspecified,
            DelayedBy = delay?.from?.ToIdentifier(this.GetSession()),
            DelayReason = delay?.reason,
            Addresses = addresses,
            ReceiptIdentifier = receiptFor,
            Extensions = InnerHandler.ToExtensions()
        };
    }

    protected virtual Event GetEvent()
    {
        return new MessageEvent {
            Origin = this.GetOrigin(addressesParser?.Recipients),
            Type = (this.GetStanza().Type?.ToEnum()).ToMessageType(),
            Processing = this.GetProcessing(delay?.timestamp),
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
        return new ErrorEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = errorParser.GetError(GetMessage())
        };
    }
}
