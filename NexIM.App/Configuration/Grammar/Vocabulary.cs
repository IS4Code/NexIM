using System;
using System.Linq;
using NexIM.Primitives;
using NexIM.Primitives.Xml;

namespace NexIM.App.Configuration.Grammar;

/// <summary>
/// Provides atomized common configuration vocabulary elements.
/// </summary>
public sealed partial class Vocabulary : XmlVocabulary
{
    public static class Standard
    {
        public static readonly Token<Enum> Empty = Token<Enum>.FromAtomized(String.Empty);

        public static readonly Token<Enum> Xmlns = Token<Enum>.FromAtomized("xmlns");
        public static readonly Token<Enum> XmlnsNs = Token<Enum>.FromAtomized("http://www.w3.org/2000/xmlns/");
        public static readonly Token<Enum> XmlNs = Token<Enum>.FromAtomized("http://www.w3.org/XML/1998/namespace");
        public static readonly Token<Enum> Lang = Token<Enum>.FromAtomized("lang");
    }

    public static readonly Vocabulary Instance = new();

    private partial void AddKeys();

    private Vocabulary()
    {
        Initialize();
    }

    protected override void Initialize()
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
}
