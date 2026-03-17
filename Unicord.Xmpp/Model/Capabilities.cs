using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Model;

[StructLayout(LayoutKind.Auto)]
public readonly record struct Capabilities
{
    // Assume these are already pre-sorted
    public IReadOnlySet<Identity> Identities { get; init; }
    public IReadOnlySet<Identity> ExplicitLanguageIdentities { get; init; }
    public IReadOnlySet<Token<DiscoFeature>> Features { get; init; }
    public IReadOnlyDictionary<Token<FieldValue>, Form> Forms { get; init; }

    static readonly XmlWriterSettings hashWriterSettings = new()
    {
        Async = false,
        CheckCharacters = false,
        CloseOutput = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = false,
        NewLineHandling = NewLineHandling.None,
        OmitXmlDeclaration = true
    };

    public string ComputeHashCode(HashAlgorithm algorithm, bool requireExplicitLanguage = false)
    {
        using var stream = new HashStream(algorithm);

        using(var writer = new StreamWriter(stream, leaveOpen: true))
        {
            // The specification is shaky about escaping, but this prevents attacks at the cost of ambiguities in the resulting hash
            using var xmlWriter = XmlWriter.Create(writer, hashWriterSettings);

            foreach(var identity in requireExplicitLanguage ? ExplicitLanguageIdentities : Identities)
            {
                Write(identity.Category.Value);
                WriteDelimiter("/");
                Write(identity.Type.Value);
                WriteDelimiter("/");
                Write(identity.Name?.LanguageTag ?? "");
                WriteDelimiter("/");
                Write(identity.Name?.Value ?? "");
                WriteDelimiter("<");
            }
            foreach(var feature in Features)
            {
                Write(feature.Value);
                WriteDelimiter("<");
            }
            foreach(var form in Forms)
            {
                Write(form.Key.Value);
                WriteDelimiter("<");
                foreach(var field in form.Value.Fields)
                {
                    Write(field.Key.Value);
                    WriteDelimiter("<");
                    foreach(var value in field.Value.Values)
                    {
                        Write(value.Value);
                        WriteDelimiter("<");
                    }
                }
            }

            void Write(string str)
            {
                xmlWriter.WriteString(str);
            }

            void WriteDelimiter(string str)
            {
                xmlWriter.WriteRaw(str);
            }
        }

        Span<byte> result = stackalloc byte[stream.HashSize];
        return Convert.ToBase64String(stream.ComputeHash(result));
    }

    public bool Equals(Capabilities other)
    {
        return
            Identities.SetEquals(other.Identities) &&
            ((Identities == ExplicitLanguageIdentities && other.Identities == other.ExplicitLanguageIdentities) || ExplicitLanguageIdentities.SetEquals(other.ExplicitLanguageIdentities)) &&
            Features.SetEquals(other.Features) &&
            Forms.Count == other.Forms.Count &&
            Forms.All(p => other.Forms.TryGetValue(p.Key, out var v) && p.Value == v);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach(var identity in Identities)
        {
            hashCode.Add(identity);
        }
        if(Identities != ExplicitLanguageIdentities)
        {
            foreach(var identity in ExplicitLanguageIdentities)
            {
                hashCode.Add(identity);
            }
        }
        foreach(var feature in Features)
        {
            hashCode.Add(feature);
        }
        foreach(var pair in Forms)
        {
            hashCode.Add(pair.Key);
            hashCode.Add(pair.Value);
        }
        return hashCode.ToHashCode();
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct Identity(LanguageTaggedString? Name, Token<DiscoCategory> Category, Token<DiscoType> Type);

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct Form(IReadOnlyDictionary<Token<FieldVariable>, Field> Fields)
    {
        public bool Equals(Form other)
        {
            return
                Fields.Count == other.Fields.Count &&
                Fields.All(p => other.Fields.TryGetValue(p.Key, out var v) && p.Value == v);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            foreach(var pair in Fields)
            {
                hashCode.Add(pair.Key);
                hashCode.Add(pair.Value);
            }
            return hashCode.ToHashCode();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct Field(Token<FieldType> Type, IReadOnlySet<Token<FieldValue>> Values)
    {
        public bool Equals(Field other)
        {
            return
                Type == other.Type &&
                Values.SetEquals(other.Values);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Type);
            foreach(var value in Values)
            {
                hashCode.Add(value);
            }
            return hashCode.ToHashCode();
        }
    }
}
