using System;
using System.Collections.Generic;
using System.Linq;
using ExCSS;
using Microsoft.Extensions.ObjectPool;

namespace NexIM.Xmpp.Protocol.Grammar;

public readonly struct InlineStyle : IDisposable
{
    static readonly StylesheetParser parser = new(tolerateInvalidValues: true);

    static readonly DefaultObjectPool<StyleDeclaration> declarationPool = new(DeclarationPoolPolicy.Instance, DeclarationPoolPolicy.BatchSize * 2);

    readonly StyleDeclaration? declarations;

    public IEnumerable<KeyValuePair<string, string>> Properties =>
        (declarations?.Declarations ?? Array.Empty<Property>())
        .Select(d => new KeyValuePair<string, string>(d.Name, d.Value));

    public InlineStyle(string style)
    {
        var declaration = declarationPool.Get();

        try
        {
            // Parse
            declaration.CssText = style;
        }
        catch when(Return())
        {
            // Return back on error
            throw;
        }
        bool Return()
        {
            declarationPool.Return(declaration);
            return false;
        }

        this.declarations = declaration;
    }

    public InlineStyle(IEnumerable<KeyValuePair<string, string>> properties)
    {
        var declaration = declarationPool.Get();

        try
        {
            declaration.Clear();
            foreach(var property in properties)
            {
                declaration.SetPropertyValue(property.Key, property.Value);
            }
        }
        catch when(Return())
        {
            // Return back on error
            throw;
        }
        bool Return()
        {
            declarationPool.Return(declaration);
            return false;
        }

        this.declarations = declaration;
    }

    public override string ToString()
    {
        return declarations?.CssText ?? "";
    }

    public void Dispose()
    {
        if(declarations is not null)
        {
            declarationPool.Return(declarations);
        }
    }

    sealed class DeclarationPoolPolicy : IPooledObjectPolicy<StyleDeclaration>
    {
        public const int BatchSize = 16;

        static readonly string rulesCss = String.Concat(Enumerable.Repeat("*{all:unset}", BatchSize));

        public static readonly DeclarationPoolPolicy Instance = new();

        private DeclarationPoolPolicy()
        {

        }

        public StyleDeclaration Create()
        {
            StyleDeclaration? first = null;

            // Create a new stylesheet
            foreach(var rule in parser.Parse(rulesCss).StyleRules)
            {
                if(first is null)
                {
                    // Return from the method
                    first = rule.Style;
                }
                else
                {
                    // Stash for next retrieval
                    declarationPool.Return(rule.Style);
                }
            }

            return first!;
        }

        public bool Return(StyleDeclaration obj)
        {
            obj.Clear();
            return true;
        }
    }
}
