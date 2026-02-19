using System;
using System.Linq;
using System.Xml;

namespace Unicord.Xmpp.Grammar;

/// <summary>
/// Provides atomized common XMPP vocabulary elements.
/// </summary>
public abstract partial class XmppVocabulary : XmlNameTable
{
    public static readonly Key Empty = new("");

    public static readonly Key Xmlns = new("xmlns");
    public static readonly Key XmlnsNs = new("http://www.w3.org/2000/xmlns/");
    public static readonly Key XmlNs = new("http://www.w3.org/XML/1998/namespace");
    public static readonly Key XmlLang = new("lang");

    public static readonly Key StreamsNs = new("http://etherx.jabber.org/streams");
    public static readonly Key JabberClientNs = new("jabber:client");

    public static readonly Key Stream = new("stream");

    public static readonly Key Message = new("message");
    public static readonly Key Presence = new("presence");
    public static readonly Key Iq = new("iq");

    public static readonly Key TypeAttr = new("type");
    public static readonly Key Id = new("id");
    public static readonly Key From = new("from");
    public static readonly Key To = new("to");
    public static readonly Key Version = new("version");

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

    public readonly record struct Key(string Value)
    {
        public static bool operator ==(string a, Key b)
        {
            return ReferenceEquals(a, b.Value);
        }

        public static bool operator !=(string a, Key b)
        {
            return !ReferenceEquals(a, b.Value);
        }

        public static bool operator ==(Key a, string b)
        {
            return ReferenceEquals(a.Value, b);
        }

        public static bool operator !=(Key a, string b)
        {
            return !ReferenceEquals(a.Value, b);
        }

        public static implicit operator string(Key key)
        {
            return key.Value;
        }
    }
}
