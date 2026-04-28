using System.Net;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal class ErrorParser : BaseDelegatingStanzaErrorHandler<XmppStanzaExceptionHandler, EmptyDisposable, ICommandContext>, ICommandHandler
{
    readonly CapturingHandler<IStanzaErrorHandler> extensionsHandler = new();

    protected sealed override XmppStanzaExceptionHandler InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    readonly ErrorType? type;
    readonly int? code;
    readonly XmppResource? by;
    LocalizedString.Builder descriptionBuilder;

    public ErrorParser(Token<ErrorType>? type, int? code, XmppResource? by)
    {
        this.type = type?.ToEnum();
        this.code = code;
        this.by = by;
    }

    protected async override ValueTask OnText(LanguageTaggedString? text)
    {
        descriptionBuilder.Add(text);
    }

    protected override ValueTask OnOther(XmlReader payloadReader)
    {
        // Extended error status goes to the extensions handler
        IStanzaErrorHandler handler = extensionsHandler;
        return handler.Other(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            await extensionsHandler.DisposeAsync();
        }
        finally
        {
            await base.DisposeAsync();
        }
    }

    public ErrorData GetError(EventData originalData)
    {
        return new() {
            ErrorCode = InnerHandler.Exception.ToErrorCode(),
            Description = descriptionBuilder.TryToString(),
            RecommendedAction = type?.ToRecommendedAction() ?? RecommendedErrorAction.Proceed,
            Reporter = by?.ToIdentifier(),
            HttpStatusCode = (HttpStatusCode?)code,
            OriginalData = originalData,
            Extensions = extensionsHandler.ToExtensions()
        };
    }
}
