global using static NexIM.Xmpp.Protocol.Constants;

namespace NexIM.Xmpp.Protocol;

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
    public const string Amp = "http://jabber.org/protocol/amp";
    public const string AmpAction = Amp + "?action=";
    public const string AmpCondition = Amp + "?condition=";
    public const string XData = "jabber:x:data";
    public const string Caps = "http://jabber.org/protocol/caps";
    public const string VCardTemp = "vcard-temp";
    public const string XmppDelay = "urn:xmpp:delay";
    public const string XmppReceipts = "urn:xmpp:receipts";
    public const string Addresses = "http://jabber.org/protocol/address";
}
