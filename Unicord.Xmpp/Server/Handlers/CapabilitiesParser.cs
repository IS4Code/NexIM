using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml;
using Unicord.Xmpp.Model;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Tools;

namespace Unicord.Xmpp.Server.Handlers;

using static Capabilities;

internal class CapabilitiesParser<TContext> : BaseDiscoInfoQueryHandler<TContext> where TContext : IPayloadHandlerContext
{
    readonly SortedSet<Identity> identities = new(Comparer.Instance);
    // Identities where non-explicit xml:lang is removed, if they differ
    SortedSet<Identity>? explicitLanguageIdentities;
    readonly SortedSet<Token<DiscoFeature>> features = new(Comparer.Instance);
    readonly SortedDictionary<Token<FieldValue>, Form> forms = new(Comparer.Instance);

    public Capabilities Capabilities => new()
    {
        Identities = identities,
        ExplicitLanguageIdentities = explicitLanguageIdentities ?? identities,
        Features = features,
        Forms = forms
    };

    protected async override ValueTask OnIdentity(LanguageTaggedString? name, Token<DiscoCategory>? category, Token<DiscoType>? type)
    {
        if(category is not { } categoryValue || type is not { } typeValue)
        {
            return;
        }

        var identity = new Identity(name, categoryValue, typeValue);

        if(name is { Explicit: false } nameValue)
        {
            // The language is not explicit - strip off
            (explicitLanguageIdentities ??= new(identities, Comparer.Instance)).Add(identity with { Name = nameValue with { LanguageTag = "" } });
        }
        else
        {
            explicitLanguageIdentities?.Add(identity);
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
            cmp = octetComparer.Compare(x.Name?.LanguageTag ?? "", y.Name?.LanguageTag ?? "");
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
