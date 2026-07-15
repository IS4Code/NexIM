using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExCSS;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json.Linq;

namespace NexIM.Xmpp.Protocol.Grammar;

public readonly struct InlineStyle : IDisposable, IReadOnlyDictionary<string, string>
{
    static readonly StylesheetParser parser = new(tolerateInvalidValues: true);

    static readonly DefaultObjectPool<StyleDeclaration> declarationPool = new(DeclarationPoolPolicy.Instance, DeclarationPoolPolicy.BatchSize * 2);

    readonly StyleDeclaration? declarations;
    IEnumerable<Property> properties => declarations?.Declarations ?? Array.Empty<Property>();

    public string this[string key] => declarations?[key] ?? "";

    public IEnumerable<string> Keys => properties.Select(static p => p.Name);
    public IEnumerable<string> Values => properties.Select(static p => p.Value);

    public int Count => declarations?.Length ?? 0;

    public InlineStyle(string style)
    {
        var declaration = declarationPool.Get();

        try
        {
            // Try parse
            declaration.CssText = style;
        }
        catch
        {
            // Return on exception
            declarationPool.Return(declaration);
            return;
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

    public void WriteTo<TFormatter>(TFormatter formatter) where TFormatter : struct, IFormatter
    {
        int index = 0;
        foreach(var prop in properties)
        {
            var (original, value) = (prop.Original, prop.Value);
            formatter.WriteDeclaration(prop.Name, value.Length < original.Length ? value : original, index++);
        }
    }

    public async ValueTask WriteToAsync<TFormatter>(TFormatter formatter) where TFormatter : struct, IFormatter
    {
        int index = 0;
        foreach(var prop in properties)
        {
            var (original, value) = (prop.Original, prop.Value);
            await formatter.WriteDeclarationAsync(prop.Name, value.Length < original.Length ? value : original, index++);
        }
    }

    public override string ToString()
    {
        if(declarations is not { Length: > 0 })
        {
            return "";
        }

        var sb = new StringBuilder();
        WriteTo(new StringBuilderFormatter(sb));
        return sb.ToString();
    }

    readonly struct StringBuilderFormatter(StringBuilder sb) : IFormatter
    {
        public void WriteDeclaration(string name, string value, int index)
        {
            if(index > 0)
            {
                sb.Append(';');
            }
            sb.Append(name);
            sb.Append(':');
            sb.Append(value);
        }

        public ValueTask WriteDeclarationAsync(string name, string value, int index)
        {
            WriteDeclaration(name, value, index);
            return default;
        }
    }

    public void Dispose()
    {
        if(declarations is not null)
        {
            declarationPool.Return(declarations);
        }
    }

    public bool ContainsKey(string key)
    {
        return !String.IsNullOrEmpty(declarations?[key]);
    }

    public bool TryGetValue(string key, out string value)
    {
        value = declarations?[key] ?? "";
        return !String.IsNullOrEmpty(value);
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return properties.Select(static p => new KeyValuePair<string, string>(p.Name, p.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
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

    public interface IFormatter
    {
        void WriteDeclaration(string name, string value, int index);
        ValueTask WriteDeclarationAsync(string name, string value, int index);
    }
}
