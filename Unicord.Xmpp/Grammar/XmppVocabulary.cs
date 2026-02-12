using System;
using System.Xml;

namespace Unicord.Xmpp.Grammar;

internal partial class XmppVocabulary : NameTable
{
    public static readonly Key Empty = new("");

    public static readonly Key Xmlns = new("xmlns");
    public static readonly Key XmlnsNs = new("http://www.w3.org/2000/xmlns/");

    public static readonly Key StreamsNs = new("http://etherx.jabber.org/streams");
    public static readonly Key JabberClientNs = new("jabber:client");

    public static readonly Key Stream = new("stream");

    public static readonly Key Message = new("message");
    public static readonly Key Presence = new("presence");
    public static readonly Key Iq = new("iq");

    public static readonly Key Type = new("type");
    public static readonly Key Id = new("id");
    public static readonly Key From = new("from");
    public static readonly Key To = new("to");
    public static readonly Key Version = new("version");

    bool allowAdding = true;
    readonly object syncRoot = new();

    private partial void AddKeys();

    public XmppVocabulary()
    {
        AddKey(Empty);
        AddKey(Xmlns);
        AddKey(XmlnsNs);

        AddKeys();

        AddKey(StreamsNs);
        AddKey(JabberClientNs);

        AddKey(Stream);

        AddKey(Message);
        AddKey(Presence);
        AddKey(Iq);

        AddKey(Type);
        AddKey(Id);
        AddKey(From);
        AddKey(To);
        AddKey(Version);

        AddKey("xml");
        AddKey("encoding");
        AddKey("standalone");
        AddKey("lang");
        AddKey("en");
        AddKey("http://www.w3.org/XML/1998/namespace");
    }

    public override string Add(char[] key, int start, int len)
    {
        return base.Get(key, start, len) ?? AddSynchronized(key, start, len);
    }

    public override string Add(string key)
    {
        return base.Get(key) ?? AddSynchronized(key);
    }

    private partial void AddKey(string key)
    {
        if (!ReferenceEquals(key, base.Add(key)))
        {
            throw new NotSupportedException("The key reference is invalid.");
        }
    }

    private string AddSynchronized(char[] key, int start, int len)
    {
        if (!allowAdding)
        {
            throw new InvalidOperationException($"{new string(key, start, len)} is not found in the table.");
        }
        lock (syncRoot)
        {
            return base.Add(key, start, len);
        }
    }

    private string AddSynchronized(string key)
    {
        if (!allowAdding)
        {
            throw new InvalidOperationException($"{key} is not found in the table.");
        }
        lock (syncRoot)
        {
            return base.Add(key);
        }
    }

    public override string? Get(char[] key, int start, int len)
    {
        return base.Get(key, start, len);
    }

    public override string? Get(string value)
    {
        return base.Get(value);
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
