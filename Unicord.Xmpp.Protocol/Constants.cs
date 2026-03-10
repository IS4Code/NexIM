global using static Unicord.Xmpp.Protocol.Constants;

namespace Unicord.Xmpp.Protocol;

static class Constants
{
    public const string IqRoster = "jabber:iq:roster";
    public const string IqAuth = "jabber:iq:auth";
    public const string ChatStates = "http://jabber.org/protocol/chatstates";
    public const string XmppTls = "urn:ietf:params:xml:ns:xmpp-tls";
    public const string Streams = "http://etherx.jabber.org/streams";
    public const string Stanzas = "urn:ietf:params:xml:ns:xmpp-stanzas";
    public const string FeaturesCompress = "http://jabber.org/features/compress";
    public const string Compression = "http://jabber.org/protocol/compress";
    public const string XmppSasl = "urn:ietf:params:xml:ns:xmpp-sasl";
    public const string XmppBind = "urn:ietf:params:xml:ns:xmpp-bind";
    public const string XmppSession = "urn:ietf:params:xml:ns:xmpp-session";
    public const string XmppPing = "urn:xmpp:ping";
    public const string DiscoInfo = "http://jabber.org/protocol/disco#info";
    public const string DiscoItems = "http://jabber.org/protocol/disco#items";
    public const string XmppTime = "urn:xmpp:time";
}
