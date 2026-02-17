using System;

namespace Unicord.Xmpp.Protocol;

public class XmppException : ApplicationException
{
    public bool Fatal { get; }

    public XmppException(string message, bool fatal) : base(message)
    {
        Fatal = fatal;
    }

    public XmppException(string message, bool fatal, Exception? innerException) : base(message, innerException)
    {
        Fatal = fatal;
    }
}
