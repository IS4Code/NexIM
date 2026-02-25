using System;
using System.Linq;
using System.Xml;
using Unicord.Server.Primitives.Xml;

namespace Unicord.Xmpp.Grammar;

/// <summary>
/// Provides atomized common XMPP vocabulary elements.
/// </summary>
public abstract partial class XmppVocabulary : XmlNameTable
{
    public static readonly Token Empty = new("");

    public static readonly Token Xmlns = new("xmlns");
    public static readonly Token XmlnsNs = new("http://www.w3.org/2000/xmlns/");
    public static readonly Token XmlNs = new("http://www.w3.org/XML/1998/namespace");
    public static readonly Token XmlLang = new("lang");

    public static readonly Token StreamsNs = new("http://etherx.jabber.org/streams");
    public static readonly Token JabberClientNs = new("jabber:client");

    public static readonly Token Stream = new("stream");

    public static readonly Token Message = new("message");
    public static readonly Token Presence = new("presence");
    public static readonly Token Iq = new("iq");

    public static readonly Token TypeAttr = new("type");
    public static readonly Token Id = new("id");
    public static readonly Token From = new("from");
    public static readonly Token To = new("to");
    public static readonly Token Version = new("version");

    private partial void AddKeys();

    public XmppVocabulary()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        AddKey(Empty);
        AddKey(Xmlns);
        AddKey(XmlnsNs);
        AddKey(XmlNs);
        AddKey(XmlLang);

        AddKeys();

        AddKey(StreamsNs);
        AddKey(JabberClientNs);

        AddKey(Stream);

        AddKey(Message);
        AddKey(Presence);
        AddKey(Iq);

        AddKey(TypeAttr);
        AddKey(Id);
        AddKey(From);
        AddKey(To);
        AddKey(Version);

        AddKey("stream:stream");
        foreach(char c in Enumerable.Range('a', ('z' - 'a') + 1))
        {
            AddKey(String.Intern(c.ToString()));
        }

        AddKey("xml");
        AddKey("encoding");
        AddKey("standalone");
    }

    private partial void AddKey(string key)
    {
        if (!ReferenceEquals(key, Add(key)))
        {
            throw new NotSupportedException("The key reference is invalid.");
        }
    }
}
