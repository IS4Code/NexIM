using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Tools;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Formats;

namespace NexIM.Xmpp.Server.Handlers;

/// <summary>
/// Handles incoming message commands.
/// </summary>
internal class Message : BaseDelegatingMessageHandler<CapturingHandler<IMessageHandler>, EmptyDisposable, ICommandContext>, ICommandHandler
{
    LocalizedString.Builder subjectBuilder, bodyBuilder;
    string? nick;
    (string? identifier, string? parent)? thread;
    ConversationState? state;
    (DateTime? timestamp, DeliveryTiming timing)? delay;
    AddressesParser<ICommandContext>? addressesParser;
    NonEmptySet<MessageRelation>.Builder messageRelationsBuilder;
    bool isNotification;

    protected sealed override CapturingHandler<IMessageHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    protected async sealed override ValueTask OnBody(LanguageTaggedString? text)
    {
        bodyBuilder.Add(text);
    }

    protected async sealed override ValueTask OnSubject(LanguageTaggedString? text)
    {
        subjectBuilder.Add(text);
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
        this.SetOnce(ref delay, (timestamp, new(from?.ToIdentifier(this.GetSession()), reason)));
    }

    protected async override ValueTask<IAddressesHandler> OnAddresses()
    {
        return addressesParser ??= this.GetHandler<AddressesParser<ICommandContext>>();
    }

    protected async override ValueTask OnReceiptRequest()
    {
        var parser = addressesParser ??= this.GetHandler<AddressesParser<ICommandContext>>();
        parser.Add(AddressRelation.DispositionNotification);
    }

    protected async override ValueTask OnReceiptResponse(string? id)
    {
        isNotification = true;
        if(id == null)
        {
            return;
        }
        messageRelationsBuilder.Add(new MessageRelation(DeliveryRelationType.DispositionNotify, null, id));
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

        if(isNotification && addresses is { } addressesSet)
        {
            if(addressesSet.ContainsKey(AddressRelation.DispositionNotification))
            {
                throw XmppStanzaException.BadRequest("A delivery notification cannot be requested for a message acknowledgment.");
            }
        }

        var content = MessageBodyCollection.Empty.Data.ToBuilder();
        if(this.bodyBuilder.TryToString() is { } bodyString)
        {
            foreach(var body in bodyString)
            {
                // TODO XHTML
                content[(MessageFormat.Plain, body.Language)] = body.Value;
            }
        }

        return new MessageData {
            Subject = subjectBuilder.TryToString(),
            Body = new(content.ToImmutable()),
            ThreadIdentifier = thread?.identifier,
            ParentThreadIdentifier = thread?.parent,
            Presentation = new(Nickname: nick),
            State = state ?? ConversationState.Unspecified,
            Timing = delay?.timing,
            AddressRelations = addresses,
            MessageRelations = messageRelationsBuilder.TryToSet(),
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
