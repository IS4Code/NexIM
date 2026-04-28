using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol.Handlers;

public abstract class XmppExceptionHandler<TException, THandler> : CapturingHandler<THandler> where TException : XmppException<THandler> where THandler : IPayloadHandler
{
    public abstract TException Exception { get; }
}

public class XmppStreamExceptionHandler : XmppExceptionHandler<XmppStreamException, IStreamErrorHandler>, IStreamErrorTextHandler
{
    LocalizedString.Builder messageBuilder;

    public sealed override XmppStreamException Exception => new(messageBuilder.TryToString(), Replay);

    ValueTask IStreamErrorTextHandler.Text(LanguageTaggedString? text)
    {
        messageBuilder.Add(text);
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

    LocalizedString.Builder messageBuilder;

    public sealed override XmppStanzaException Exception => new(Type, Code, messageBuilder.TryToString(), Replay);

    ValueTask IStanzaErrorTextHandler.Text(LanguageTaggedString? text)
    {
        messageBuilder.Add(text);
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
