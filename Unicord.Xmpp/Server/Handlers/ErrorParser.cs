using System.Net;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal class ErrorParser : BaseDelegatingStanzaErrorHandler<XmppStanzaExceptionHandler, EmptyDisposable, ICommandContext>, ICommandHandler
{
    readonly CapturingHandler<IStanzaErrorHandler> extensionsHandler = new();

    protected sealed override XmppStanzaExceptionHandler InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    readonly ErrorType? type;
    readonly int? code;
    readonly XmppResource? by;
    LocalizedString description;

    public ErrorParser(Token<ErrorType>? type, int? code, XmppResource? by)
    {
        this.type = type?.ToEnum();
        this.code = code;
        this.by = by;
    }

    protected async override ValueTask OnText(LanguageTaggedString? text)
    {
        description = description.Add(text);
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
            Description = description,
            RecommendedAction = type?.ToRecommendedAction() ?? RecommendedErrorAction.Proceed,
            Reporter = by?.ToIdentifier(),
            HttpStatusCode = (HttpStatusCode?)code,
            OriginalData = originalData,
            Extensions = extensionsHandler.ToExtensions()
        };
    }
}
