using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Protocol;

public abstract class XmppException : ApplicationException
{
    protected string DefaultLanguage {
        get {
            var culture = CultureInfo.CurrentUICulture;
            if(culture == CultureInfo.InvariantCulture)
            {
                return "en";
            }
            return culture.TwoLetterISOLanguageName;
        }
    }

    // May be absent if no error message was provide during construction.
    public override string? Message { get; }

    internal XmppException()
    {

    }

    public XmppException(string? message) : base(message)
    {
        Message = message;
    }

    public XmppException(string? message, Exception? innerException) : base(message, innerException)
    {
        Message = message;
    }
}

public abstract class XmppException<THandler> : XmppException where THandler : IPayloadHandler
{
    readonly Func<THandler, ValueTask> details;

    public XmppException(Func<THandler, ValueTask> details)
    {
        this.details = details;
    }

    public XmppException(string? message, Func<THandler, ValueTask> details) : base(message)
    {
        this.details = details;
    }

    public XmppException(string? message, Func<THandler, ValueTask> details, Exception? innerException) : base(message, innerException)
    {
        this.details = details;
    }

    public override string ToString()
    {
        var output = new StringBuilder();
        var encoder = new Encoder(output);
        if(encoder is not THandler handler)
        {
            return base.ToString();
        }
        var task = Output(handler);
        if(!task.IsCompletedSuccessfully)
        {
            task.AsTask().GetAwaiter().GetResult();
        }
        encoder.Close();
        return output.ToString();
    }

    public virtual ValueTask Output(THandler handler) => details(handler);

    sealed class Encoder : XmppEncoder
    {
        static readonly XmlWriterSettings settings = new()
        {
            Async = true,
            CheckCharacters = false,
            CloseOutput = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Indent = false,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            NewLineHandling = NewLineHandling.Entitize,
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true
        };

        protected override XmlWriter Writer { get; }

        protected override CancellationToken CancellationToken => default;

        public Encoder(StringBuilder output)
        {
            Writer = XmlWriter.Create(output, settings);
        }

        public void Close()
        {
            Writer.Dispose();
        }

        protected override ValueTask<XmppEncoder> ForkInner()
        {
            return new(this);
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}

public class XmppStreamException : XmppException<IStreamErrorHandler>
{
    public XmppStreamException(Func<IStreamErrorHandler, ValueTask> details) : base(details)
    {

    }

    public XmppStreamException(string? message, Func<IStreamErrorHandler, ValueTask> details) : base(message, details)
    {

    }

    public override async ValueTask Output(IStreamErrorHandler handler)
    {
        await base.Output(handler);
        if(!String.IsNullOrWhiteSpace(Message))
        {
            await handler.Text(Message, DefaultLanguage);
        }
    }

    public static XmppStreamException BadFormat(string? message = null) => Create(message, static h => h.BadFormat());
    public static XmppStreamException BadNamespacePrefix(string? message = null) => Create(message, static h => h.BadNamespacePrefix());
    public static XmppStreamException Conflict(string? message = null) => Create(message, static h => h.Conflict());
    public static XmppStreamException ConnectionTimeout(string? message = null) => Create(message, static h => h.ConnectionTimeout());
    public static XmppStreamException HostGone(string? message = null) => Create(message, static h => h.HostGone());
    public static XmppStreamException HostUnknown(string? message = null) => Create(message, static h => h.HostUnknown());
    public static XmppStreamException ImproperAddressing(string? message = null) => Create(message, static h => h.ImproperAddressing());
    public static XmppStreamException InternalServerError(string? message = null) => Create(message, static h => h.InternalServerError());
    public static XmppStreamException InvalidFrom(string? message = null) => Create(message, static h => h.InvalidFrom());
    public static XmppStreamException InvalidId(string? message = null) => Create(message, static h => h.InvalidId());
    public static XmppStreamException InvalidNamespace(string? message = null) => Create(message, static h => h.InvalidNamespace());
    public static XmppStreamException InvalidXml(string? message = null) => Create(message, static h => h.InvalidXml());
    public static XmppStreamException NotAuthorized(string? message = null) => Create(message, static h => h.NotAuthorized());
    public static XmppStreamException PolicyViolation(string? message = null) => Create(message, static h => h.PolicyViolation());
    public static XmppStreamException RemoteConnectionFailed(string? message = null) => Create(message, static h => h.RemoteConnectionFailed());
    public static XmppStreamException ResourceConstraint(string? message = null) => Create(message, static h => h.ResourceConstraint());
    public static XmppStreamException RestrictedXml(string? message = null) => Create(message, static h => h.RestrictedXml());
    public static XmppStreamException SeeOtherHost(string? host, string? message = null) => Create(message, h => h.SeeOtherHost(host));
    public static XmppStreamException SystemShutdown(string? message = null) => Create(message, static h => h.SystemShutdown());
    public static XmppStreamException UndefinedCondition(string? message = null) => Create(message, static h => h.UndefinedCondition());
    public static XmppStreamException UnsupportedEncoding(string? message = null) => Create(message, static h => h.UnsupportedEncoding());
    public static XmppStreamException UnsupportedStanzaType(string? message = null) => Create(message, static h => h.UnsupportedStanzaType());
    public static XmppStreamException UnsupportedVersion(string? message = null) => Create(message, static h => h.UnsupportedVersion());
    public static XmppStreamException XmlNotWellFormed(string? message = null) => Create(message, static h => h.XmlNotWellFormed());

    private static XmppStreamException Create(string? message, Func<IStreamErrorHandler, ValueTask> details)
    {
        return message != null ? new(message, details) : new(details);
    }
}

public class XmppStanzaException : XmppException<IStanzaErrorHandler>
{
    public string? Type { get; }

    public XmppStanzaException(string? type, Func<IStanzaErrorHandler, ValueTask> details) : base(details)
    {
        Type = type;
    }

    public XmppStanzaException(string? type, string? message, Func<IStanzaErrorHandler, ValueTask> details) : base(message, details)
    {
        Type = type;
    }

    public override async ValueTask Output(IStanzaErrorHandler handler)
    {
        await base.Output(handler);
        if(!String.IsNullOrWhiteSpace(Message))
        {
            await handler.Text(Message, DefaultLanguage);
        }
    }

    public static XmppStanzaException BadRequest(string? message = null) => Create("modify", message, static h => h.BadRequest());
    public static XmppStanzaException Conflict(string? message = null) => Create("cancel", message, static h => h.Conflict());
    public static XmppStanzaException FeatureNotImplemented(string? message = null) => Create("cancel", message, static h => h.FeatureNotImplemented());
    public static XmppStanzaException Forbidden(string? message = null) => Create("auth", message, static h => h.Forbidden());
    public static XmppStanzaException Gone(string? newAddress, string? message = null) => Create("modify", message, h => h.Gone(newAddress));
    public static XmppStanzaException InternalServerError(string? message = null) => Create("wait", message, static h => h.InternalServerError());
    public static XmppStanzaException ItemNotFound(string? message = null) => Create("cancel", message, static h => h.ItemNotFound());
    public static XmppStanzaException JidMalformed(string? message = null) => Create("modify", message, static h => h.JidMalformed());
    public static XmppStanzaException NotAcceptable(string? message = null) => Create("modify", message, static h => h.NotAcceptable());
    public static XmppStanzaException NotAllowed(string? message = null) => Create("cancel", message, static h => h.NotAllowed());
    public static XmppStanzaException NotAuthorized(string? message = null) => Create("auth", message, static h => h.NotAuthorized());
    public static XmppStanzaException PaymentRequired(string? message = null) => Create("auth", message, static h => h.PaymentRequired());
    public static XmppStanzaException RecipientUnavailable(string? message = null) => Create("wait", message, static h => h.RecipientUnavailable());
    public static XmppStanzaException Redirect(string? alternateAddress, string? message = null) => Create("modify", message, h => h.Redirect(alternateAddress));
    public static XmppStanzaException RegistrationRequired(string? message = null) => Create("auth", message, static h => h.RegistrationRequired());
    public static XmppStanzaException RemoteServerNotFound(string? message = null) => Create("cancel", message, static h => h.RemoteServerNotFound());
    public static XmppStanzaException RemoteServerTimeout(string? message = null) => Create("wait", message, static h => h.RemoteServerTimeout());
    public static XmppStanzaException ResourceConstraint(string? message = null) => Create("wait", message, static h => h.ResourceConstraint());
    public static XmppStanzaException ServiceUnavailable(string? message = null) => Create("cancel", message, static h => h.ServiceUnavailable());
    public static XmppStanzaException SubscriptionRequired(string? message = null) => Create("auth", message, static h => h.SubscriptionRequired());
    public static XmppStanzaException UndefinedCondition(string? type, string? message = null) => Create(type, message, static h => h.UndefinedCondition());
    public static XmppStanzaException UnexpectedRequest(string? message = null) => Create("wait", message, static h => h.UnexpectedRequest());

    private static XmppStanzaException Create(string type, string? message, Func<IStanzaErrorHandler, ValueTask> details)
    {
        return message != null ? new(type, message, details) : new(type, details);
    }
}
