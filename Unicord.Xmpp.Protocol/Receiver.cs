using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol;

public interface IXmppHandler : IStanzaHandler
{
    string? StreamIdentifier { get; }
    XmppResource? LocalResource { get; }
    XmppResource? RemoteResource { get; set; }
}

public interface IXmppReceiver
{
    ValueTask<IStanzaHandler> Connected(IXmppHandler session);
}
