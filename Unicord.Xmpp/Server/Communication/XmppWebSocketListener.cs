using System.Collections.Generic;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

public abstract class XmppWebSocketListener<TConnection> : XmppServerListener<TConnection, XmppFrameSession>
{
    public abstract ICollection<string> Prefixes { get; }

    protected override ConformanceLevel ConformanceLevel => ConformanceLevel.Fragment;

    public XmppWebSocketListener(IXmppReceiver<XmppFrameSession> receiver) : base(receiver)
    {

    }
}
