using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Model;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Tools;

namespace NexIM.Xmpp.Server.Handlers;

using static Capabilities;

internal class CapabilitiesParser<TContext> : BaseDiscoInfoQueryHandler<TContext> where TContext : IPayloadHandlerContext
{
    readonly SortedSet<Identity> identities = new(Comparer.Instance);
    readonly SortedSet<Token<DiscoFeature>> features = new(Comparer.Instance);
    readonly SortedDictionary<Token<FieldValue>, Form> forms = new(Comparer.Instance);

    /// <summary>
    /// Identities where non-explicit <c>xml:lang</c> is removed, if they differ from <see cref="identities"/>.
    /// </summary>
    /// <remarks>
    /// The <c>xml:lang</c> attribute affects all texts within the element it is attached to,
    /// including descendant elements, and thus all identities. However, some clients
    /// do not consider an implicit <c>xml:lang</c> in the capabilities hash,
    /// which means its effect must be disregarded before the capabilities are processed.
    /// </remarks>
    SortedSet<Identity>? alternateIdentities;

    protected async override ValueTask OnIdentity(LanguageTaggedString? name, Token<DiscoCategory>? category, Token<DiscoType>? type)
    {
        if(category is not { } categoryValue || type is not { } typeValue)
        {
            return;
        }

        var identity = new Identity(name, categoryValue, typeValue);

        if(name is { Explicit: false } nameValue)
        {
            // The language is not explicit - strip off for alternate
            (alternateIdentities ??= new(identities, Comparer.Instance)).Add(new(nameValue with { Language = default }, categoryValue, typeValue));
        }
        else
        {
            alternateIdentities?.Add(identity);
        }
        identities.Add(identity);
    }

    protected async override ValueTask OnFeature(Token<DiscoFeature>? feature)
    {
        if(feature is not { } featureValue)
        {
            return;
        }
        features.Add(featureValue);
    }

    protected async override ValueTask<IFormHandler> OnDataForm(Token<FormType>? type)
    {
        return new FormParser(this) { Context = Context };
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        return default;
    }

    public static bool IsSupportedHashAlgorithm(Token<CapabilitiesHash> hashAlgorithm)
    {
        return hashAlgorithm.ToEnum() is CapabilitiesHash.Sha1;
    }

    public Capabilities GetCapabilities(Token<CapabilitiesHash> hashAlgorithm, string expectedHash)
    {
        if(hashAlgorithm.ToEnum() != CapabilitiesHash.Sha1)
        {
            // Unknown hash algorithm
            return new() {
                Verified = false,
                Identities = identities,
                Features = features,
                Forms = forms
            };
        }

        using var sha1 = SHA1.Create();

        var computedHash = ComputeHashCode(sha1, identities, features, forms);

        bool verified;
        if(computedHash == expectedHash)
        {
            verified = true;
        }
        else if(
            alternateIdentities is { } alternate &&
            expectedHash == ComputeHashCode(sha1, alternate, features, forms)
        )
        {
            // Hash matches when the alternate identities are used
            return new() {
                Verified = true,
                Identities = alternate,
                Features = features,
                Forms = forms
            };
        }
        else
        {
            verified = false;
        }

        return new() {
            Verified = verified,
            Identities = identities,
            Features = features,
            Forms = forms
        };
    }

    public override ValueTask DisposeAsync()
    {
        return default;
    }

    sealed class FormParser(CapabilitiesParser<TContext> parent) : FormHandler<TContext>
    {
        Token<FieldValue>? formType;
        readonly SortedDictionary<Token<FieldVariable>, Field> fields = new(Comparer.Instance);

        protected async override ValueTask<IFieldHandler> OnField(Token<FieldVariable>? variable, Token<FieldType>? type, LanguageTaggedString? label)
        {
            if(variable?.ToEnum() == FieldVariable.FormType)
            {
                // Retrieve the form type
                return new FormTypeParser(this) { Context = Context };
            }
            else
            {
                // Store the variable values
                var values = new SortedSet<Token<FieldValue>>(Comparer.Instance);
                fields[variable ?? default] = new(type ?? default, values);
                return new ValueFieldParser(values) { Context = Context };
            }
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader)
        {
            return default;
        }

        public async override ValueTask DisposeAsync()
        {
            if(formType is { } type)
            {
                // Expose the form by its type
                parent.forms[type] = new(fields);
            }
        }

        abstract class FieldParser : FieldHandler<TContext>
        {
            protected abstract override ValueTask OnValue(Token<FieldValue>? value);

            protected sealed override ValueTask OnUnrecognized(XmlReader payloadReader)
            {
                return default;
            }

            public sealed override ValueTask DisposeAsync()
            {
                return default;
            }
        }

        sealed class ValueFieldParser(SortedSet<Token<FieldValue>> values) : FieldParser
        {
            protected async override ValueTask OnValue(Token<FieldValue>? value)
            {
                // Add to the set
                values.Add(value ?? default);
            }
        }

        sealed class FormTypeParser(FormParser parent) : FieldParser
        {
            protected async override ValueTask OnValue(Token<FieldValue>? value)
            {
                // Set the form type
                parent.formType = value ?? default;
            }
        }
    }

    sealed class Comparer : IComparer<Identity>, IComparer<Token<DiscoFeature>>, IComparer<Token<FieldVariable>>, IComparer<Token<FieldValue>>
    {
        static readonly EncodedStringComparer octetComparer = Utf8StringComparer.Instance;

        public static readonly Comparer Instance = new();

        private Comparer()
        {

        }

        public int Compare(Identity x, Identity y)
        {
            int cmp = octetComparer.Compare(x.Category.Value, y.Category.Value);
            if(cmp != 0)
            {
                return cmp;
            }
            cmp = octetComparer.Compare(x.Type.Value, y.Type.Value);
            if(cmp != 0)
            {
                return cmp;
            }
            cmp = octetComparer.Compare((x.Name?.Language ?? default).Value, (y.Name?.Language ?? default).Value);
            if(cmp != 0)
            {
                return cmp;
            }
            // Technically invalid
            return octetComparer.Compare(x.Name?.Value ?? "", y.Name?.Value ?? "");
        }

        public int Compare(Token<DiscoFeature> x, Token<DiscoFeature> y)
        {
            return octetComparer.Compare(x.Value, y.Value);
        }

        public int Compare(Token<FieldVariable> x, Token<FieldVariable> y)
        {
            return octetComparer.Compare(x.Value, y.Value);
        }

        public int Compare(Token<FieldValue> x, Token<FieldValue> y)
        {
            return octetComparer.Compare(x.Value, y.Value);
        }
    }
}
