using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml;
using Unicord.Primitives;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Model;

public record Capabilities : ICapabilities
{
    public required bool Verified { get; init; }

    public required IReadOnlySet<Identity> Identities { get; init; }
    public required IReadOnlySet<Token<DiscoFeature>> Features { get; init; }
    public required IReadOnlyDictionary<Token<FieldValue>, Form> Forms { get; init; }

    internal static string ComputeHashCode(HashAlgorithm algorithm, IReadOnlySet<Identity> identities, IReadOnlySet<Token<DiscoFeature>> features, IReadOnlyDictionary<Token<FieldValue>, Form> forms)
    {
        using var stream = new HashStream(algorithm);

        using(var writer = new StreamWriter(stream, leaveOpen: true))
        {
            // Write data directly to the hasher
            Serialize(writer, identities, features, forms);
        }

        Span<byte> result = stackalloc byte[stream.HashSize];
        return Convert.ToBase64String(stream.ComputeHash(result));
    }

    static readonly XmlWriterSettings textWriterSettings = new()
    {
        Async = false,
        CheckCharacters = false,
        CloseOutput = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = false,
        NewLineHandling = NewLineHandling.None,
        OmitXmlDeclaration = true
    };

    private static void Serialize(TextWriter writer, IReadOnlySet<Identity> identities, IReadOnlySet<Token<DiscoFeature>> features, IReadOnlyDictionary<Token<FieldValue>, Form> forms)
    {
        // The specification is shaky about escaping, but this prevents attacks at the cost of ambiguities in the resulting hash
        using var xmlWriter = XmlWriter.Create(writer, textWriterSettings);

        foreach(var identity in identities)
        {
            Write(identity.Category.Value);
            WriteDelimiter("/");
            Write(identity.Type.Value);
            WriteDelimiter("/");
            Write(identity.Name?.Language.Value ?? "");
            WriteDelimiter("/");
            Write(identity.Name?.Value ?? "");
            WriteDelimiter("<");
        }
        foreach(var feature in features)
        {
            Write(feature.Value);
            WriteDelimiter("<");
        }
        foreach(var form in forms)
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

    public virtual bool Equals(Capabilities? other)
    {
        return
            other is not null &&
            Identities.SetEquals(other.Identities) &&
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

    public override string ToString()
    {
        using var writer = new StringWriter();
        Serialize(writer, Identities, Features, Forms);
        return writer.ToString();
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
