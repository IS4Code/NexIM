using System;
using System.Linq;
using Unicord.Primitives.Xml;

namespace Unicord.Xmpp.Protocol.Grammar;

/// <summary>
/// Provides atomized common XMPP vocabulary elements.
/// </summary>
public abstract partial class Vocabulary : XmlMemoryNameTable
{
    public static class Standard
    {
        public static readonly Token<Enum> Empty = Token<Enum>.FromAtomized(String.Empty);

        public static readonly Token<Enum> Xmlns = Token<Enum>.FromAtomized("xmlns");
        public static readonly Token<Enum> XmlnsNs = Token<Enum>.FromAtomized("http://www.w3.org/2000/xmlns/");
        public static readonly Token<Enum> XmlNs = Token<Enum>.FromAtomized("http://www.w3.org/XML/1998/namespace");
        public static readonly Token<Enum> Lang = Token<Enum>.FromAtomized("lang");

        public static readonly Token<Enum> StreamsNs = Token<Enum>.FromAtomized("http://etherx.jabber.org/streams");
        public static readonly Token<Enum> JabberClientNs = Token<Enum>.FromAtomized("jabber:client");
        public static readonly Token<Enum> FramingNs = Token<Enum>.FromAtomized("urn:ietf:params:xml:ns:xmpp-framing");

        public static readonly Token<Enum> Stream = Token<Enum>.FromAtomized("stream");

        public static readonly Token<Enum> Message = Token<Enum>.FromAtomized("message");
        public static readonly Token<Enum> Presence = Token<Enum>.FromAtomized("presence");
        public static readonly Token<Enum> InfoQuery = Token<Enum>.FromAtomized("iq");

        public static readonly Token<Enum> Type = Token<Enum>.FromAtomized("type");
        public static readonly Token<Enum> Id = Token<Enum>.FromAtomized("id");
        public static readonly Token<Enum> From = Token<Enum>.FromAtomized("from");
        public static readonly Token<Enum> To = Token<Enum>.FromAtomized("to");
        public static readonly Token<Enum> Version = Token<Enum>.FromAtomized("version");

        public static readonly Token<Enum> Open = Token<Enum>.FromAtomized("open");
        public static readonly Token<Enum> Close = Token<Enum>.FromAtomized("close");
    }

    private partial void AddKeys();

    public Vocabulary()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        AddKey(Standard.Empty.Value);
        AddKey(Standard.Xmlns.Value);
        AddKey(Standard.XmlnsNs.Value);
        AddKey(Standard.XmlNs.Value);
        AddKey(Standard.Lang.Value);

        AddKeys();

        AddKey(Standard.StreamsNs.Value);
        AddKey(Standard.JabberClientNs.Value);
        AddKey(Standard.FramingNs.Value);

        AddKey(Standard.Stream.Value);

        AddKey(Standard.Message.Value);
        AddKey(Standard.Presence.Value);
        AddKey(Standard.InfoQuery.Value);

        AddKey(Standard.Type.Value);
        AddKey(Standard.Id.Value);
        AddKey(Standard.From.Value);
        AddKey(Standard.To.Value);
        AddKey(Standard.Version.Value);

        AddKey(Standard.Open.Value);
        AddKey(Standard.Close.Value);

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
