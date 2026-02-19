using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol;

public abstract class XmppException : ApplicationException
{
    internal XmppException()
    {

    }

    public XmppException(string message) : base(message)
    {

    }

    public XmppException(string message, Exception? innerException) : base(message, innerException)
    {

    }
}

public abstract class XmppException<THandler> : XmppException where THandler : IPayloadHandler
{
    readonly Func<THandler, ValueTask> details;

    public XmppException(Func<THandler, ValueTask> details)
    {
        this.details = details;
    }

    public XmppException(string message, Func<THandler, ValueTask> details) : base(message)
    {
        this.details = details;
    }

    public XmppException(string message, Func<THandler, ValueTask> details, Exception? innerException) : base(message, innerException)
    {
        this.details = details;
    }

    public virtual ValueTask Output(THandler handler) => details(handler);
}

public class XmppStreamException : XmppException<IStreamErrorHandler>
{
    public XmppStreamException(Func<IStreamErrorHandler, ValueTask> details) : base(details)
    {

    }

    public XmppStreamException(string message, Func<IStreamErrorHandler, ValueTask> details) : base(message, details)
    {

    }

    public override async ValueTask Output(IStreamErrorHandler handler)
    {
        await base.Output(handler);
        if(!String.IsNullOrWhiteSpace(Message))
        {
            await handler.Text(Message, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }
    }

    public static XmppStreamException BadFormat(string? message = null) => message != null ? new(message, static h => h.BadFormat()) : new(static h => h.BadFormat());
    public static XmppStreamException BadNamespacePrefix(string? message = null) => message != null ? new(message, static h => h.BadNamespacePrefix()) : new(static h => h.BadNamespacePrefix());
    public static XmppStreamException Conflict(string? message = null) => message != null ? new(message, static h => h.Conflict()) : new(static h => h.Conflict());
    public static XmppStreamException ConnectionTimeout(string? message = null) => message != null ? new(message, static h => h.ConnectionTimeout()) : new(static h => h.ConnectionTimeout());
    public static XmppStreamException HostGone(string? message = null) => message != null ? new(message, static h => h.HostGone()) : new(static h => h.HostGone());
    public static XmppStreamException HostUnknown(string? message = null) => message != null ? new(message, static h => h.HostUnknown()) : new(static h => h.HostUnknown());
    public static XmppStreamException ImproperAddressing(string? message = null) => message != null ? new(message, static h => h.ImproperAddressing()) : new(static h => h.ImproperAddressing());
    public static XmppStreamException InternalServerError(string? message = null) => message != null ? new(message, static h => h.InternalServerError()) : new(static h => h.InternalServerError());
    public static XmppStreamException InvalidFrom(string? message = null) => message != null ? new(message, static h => h.InvalidFrom()) : new(static h => h.InvalidFrom());
    public static XmppStreamException InvalidId(string? message = null) => message != null ? new(message, static h => h.InvalidId()) : new(static h => h.InvalidId());
    public static XmppStreamException InvalidNamespace(string? message = null) => message != null ? new(message, static h => h.InvalidNamespace()) : new(static h => h.InvalidNamespace());
    public static XmppStreamException InvalidXml(string? message = null) => message != null ? new(message, static h => h.InvalidXml()) : new(static h => h.InvalidXml());
    public static XmppStreamException NotAuthorized(string? message = null) => message != null ? new(message, static h => h.NotAuthorized()) : new(static h => h.NotAuthorized());
    public static XmppStreamException PolicyViolation(string? message = null) => message != null ? new(message, static h => h.PolicyViolation()) : new(static h => h.PolicyViolation());
    public static XmppStreamException RemoteConnectionFailed(string? message = null) => message != null ? new(message, static h => h.RemoteConnectionFailed()) : new(static h => h.RemoteConnectionFailed());
    public static XmppStreamException ResourceConstraint(string? message = null) => message != null ? new(message, static h => h.ResourceConstraint()) : new(static h => h.ResourceConstraint());
    public static XmppStreamException RestrictedXml(string? message = null) => message != null ? new(message, static h => h.RestrictedXml()) : new(static h => h.RestrictedXml());
    public static XmppStreamException SeeOtherHost(string? host, string? message = null) => message != null ? new(message, h => h.SeeOtherHost(host)) : new(h => h.SeeOtherHost(host));
    public static XmppStreamException SystemShutdown(string? message = null) => message != null ? new(message, static h => h.SystemShutdown()) : new(static h => h.SystemShutdown());
    public static XmppStreamException UndefinedCondition(string? message = null) => message != null ? new(message, static h => h.UndefinedCondition()) : new(static h => h.UndefinedCondition());
    public static XmppStreamException UnsupportedEncoding(string? message = null) => message != null ? new(message, static h => h.UnsupportedEncoding()) : new(static h => h.UnsupportedEncoding());
    public static XmppStreamException UnsupportedStanzaType(string? message = null) => message != null ? new(message, static h => h.UnsupportedStanzaType()) : new(static h => h.UnsupportedStanzaType());
    public static XmppStreamException UnsupportedVersion(string? message = null) => message != null ? new(message, static h => h.UnsupportedVersion()) : new(static h => h.UnsupportedVersion());
    public static XmppStreamException XmlNotWellFormed(string? message = null) => message != null ? new(message, static h => h.XmlNotWellFormed()) : new(static h => h.XmlNotWellFormed());
}

public class XmppStanzaException : XmppException<IStanzaErrorHandler>
{
    public string? Type { get; }

    public XmppStanzaException(string? type, Func<IStanzaErrorHandler, ValueTask> details) : base(details)
    {
        Type = type;
    }

    public XmppStanzaException(string? type, string message, Func<IStanzaErrorHandler, ValueTask> details) : base(message, details)
    {
        Type = type;
    }

    public override async ValueTask Output(IStanzaErrorHandler handler)
    {
        await base.Output(handler);
        if(!String.IsNullOrWhiteSpace(Message))
        {
            await handler.Text(Message, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }
    }

    public static XmppStanzaException BadRequest(string? message = null) => message != null ? new("modify", message, static h => h.BadRequest()) : new("modify", static h => h.BadRequest());
    public static XmppStanzaException Conflict(string? message = null) => message != null ? new("cancel", message, static h => h.Conflict()) : new("cancel", static h => h.Conflict());
    public static XmppStanzaException FeatureNotImplemented(string? message = null) => message != null ? new("cancel", message, static h => h.FeatureNotImplemented()) : new("cancel", static h => h.FeatureNotImplemented());
    public static XmppStanzaException Forbidden(string? message = null) => message != null ? new("auth", message, static h => h.Forbidden()) : new("auth", static h => h.Forbidden());
    public static XmppStanzaException Gone(string? newAddress, string? message = null) => message != null ? new("modify", message, h => h.Gone(newAddress)) : new("modify", h => h.Gone(newAddress));
    public static XmppStanzaException InternalServerError(string? message = null) => message != null ? new("wait", message, static h => h.InternalServerError()) : new("wait", static h => h.InternalServerError());
    public static XmppStanzaException ItemNotFound(string? message = null) => message != null ? new("cancel", message, static h => h.ItemNotFound()) : new("cancel", static h => h.ItemNotFound());
    public static XmppStanzaException JidMalformed(string? message = null) => message != null ? new("modify", message, static h => h.JidMalformed()) : new("modify", static h => h.JidMalformed());
    public static XmppStanzaException NotAcceptable(string? message = null) => message != null ? new("modify", message, static h => h.NotAcceptable()) : new("modify", static h => h.NotAcceptable());
    public static XmppStanzaException NotAllowed(string? message = null) => message != null ? new("cancel", message, static h => h.NotAllowed()) : new("cancel", static h => h.NotAllowed());
    public static XmppStanzaException NotAuthorized(string? message = null) => message != null ? new("auth", message, static h => h.NotAuthorized()) : new("auth", static h => h.NotAuthorized());
    public static XmppStanzaException PaymentRequired(string? message = null) => message != null ? new("auth", message, static h => h.PaymentRequired()) : new("auth", static h => h.PaymentRequired());
    public static XmppStanzaException RecipientUnavailable(string? message = null) => message != null ? new("wait", message, static h => h.RecipientUnavailable()) : new("wait", static h => h.RecipientUnavailable());
    public static XmppStanzaException Redirect(string? alternateAddress, string? message = null) => message != null ? new("modify", message, h => h.Redirect(alternateAddress)) : new("modify", h => h.Redirect(alternateAddress));
    public static XmppStanzaException RegistrationRequired(string? message = null) => message != null ? new("auth", message, static h => h.RegistrationRequired()) : new("auth", static h => h.RegistrationRequired());
    public static XmppStanzaException RemoteServerNotFound(string? message = null) => message != null ? new("cancel", message, static h => h.RemoteServerNotFound()) : new("cancel", static h => h.RemoteServerNotFound());
    public static XmppStanzaException RemoteServerTimeout(string? message = null) => message != null ? new("wait", message, static h => h.RemoteServerTimeout()) : new("wait", static h => h.RemoteServerTimeout());
    public static XmppStanzaException ResourceConstraint(string? message = null) => message != null ? new("wait", message, static h => h.ResourceConstraint()) : new("wait", static h => h.ResourceConstraint());
    public static XmppStanzaException ServiceUnavailable(string? message = null) => message != null ? new("cancel", message, static h => h.ServiceUnavailable()) : new("cancel", static h => h.ServiceUnavailable());
    public static XmppStanzaException SubscriptionRequired(string? message = null) => message != null ? new("auth", message, static h => h.SubscriptionRequired()) : new("auth", static h => h.SubscriptionRequired());
    public static XmppStanzaException UndefinedCondition(string? type, string? message = null) => message != null ? new(type, message, static h => h.UndefinedCondition()) : new(type, static h => h.UndefinedCondition());
    public static XmppStanzaException UnexpectedRequest(string? message = null) => message != null ? new("wait", message, static h => h.UnexpectedRequest()) : new("wait", static h => h.UnexpectedRequest());
}
