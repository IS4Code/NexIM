using System;
using System.Linq;
using NexIM.Primitives;
using NexIM.Primitives.Xml;

namespace NexIM.App.Configuration.Grammar;

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

        foreach(var c in Enumerable.Range('a', ('z' - 'a') + 1))
        {
            AddKey(String.Intern(((char)c).ToString()));
        }

        AddKey("xml");
        AddKey("encoding");
        AddKey("standalone");
    }

    private partial void AddKey(string key)
    {
        if(!ReferenceEquals(key, Add(key)))
        {
            throw new NotSupportedException("The key reference is invalid.");
        }
    }
}
