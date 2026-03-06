using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;

namespace Unicord.Xmpp.Protocol.Handlers;

public abstract class XmppExceptionHandler<TException, THandler> : CapturingHandler<THandler> where TException : XmppException<THandler> where THandler : IPayloadHandler
{
    public abstract TException Exception { get; }
}

public class XmppStreamExceptionHandler : XmppExceptionHandler<XmppStreamException, IStreamErrorHandler>, IStreamErrorTextHandler
{
    LocalizedString message;

    public sealed override XmppStreamException Exception => new(message, Replay);

    ValueTask IStreamErrorTextHandler.Text(LanguageTaggedString? text)
    {
        message.Add(text, LanguageTaggedString.DefaultLanguage);
        return default;
    }

    ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        return default;
    }
}

public class XmppStanzaExceptionHandler : XmppExceptionHandler<XmppStanzaException, IStanzaErrorHandler>, IStanzaErrorTextHandler
{
    public ErrorType? Type { get; }
    public int? Code { get; }

    LocalizedString message;

    public sealed override XmppStanzaException Exception => new(Type, Code, message, Replay);

    ValueTask IStanzaErrorTextHandler.Text(LanguageTaggedString? text)
    {
        message.Add(text, LanguageTaggedString.DefaultLanguage);
        return default;
    }

    ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        return default;
    }
}

public class XmppSaslExceptionHandler : XmppExceptionHandler<XmppSaslException, ISaslFailureHandler>, IPayloadHandler
{
    public sealed override XmppSaslException Exception => new(Replay);

    ValueTask IPayloadHandler.Other(XmlReader payloadReader)
    {
        return default;
    }
}
