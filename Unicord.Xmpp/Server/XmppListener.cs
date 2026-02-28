using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public abstract class XmppListener<TSession>(IXmppReceiver<TSession> receiver) where TSession : XmppXmlSession
{
    protected IXmppReceiver<TSession> Receiver => receiver;

    public abstract Task RunAsync(CancellationToken cancellationToken = default);

    protected bool GetXmppException<TException>(Exception e, [MaybeNullWhen(false)] out TException xmppException) where TException : XmppException
    {
        switch(e)
        {
            case TException xe:
                xmppException = xe;
                return true;
            case { InnerException: { } inner } when GetXmppException(inner, out xmppException):
                return true;
            default:
                xmppException = null;
                return false;
        }
    }
}
