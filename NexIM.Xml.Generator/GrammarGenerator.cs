using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NexIM.Xml.Generator;

[Generator]
public sealed partial class GrammarGenerator : IIncrementalGenerator
{
    const string indent = "    ";

    const string grammarNs = nameof(NexIM) + ".Primitives.Xml.Grammar";
    const string complexTypeAttributeSimpleName = "ComplexType";
    const string simpleTypeAttributeSimpleName = "SimpleType";
    const string complexTypeAttributeFullName = grammarNs + "." + complexTypeAttributeSimpleName + "Attribute";
    const string simpleTypeAttributeFullName = grammarNs + "." + simpleTypeAttributeSimpleName + "Attribute";
    const string namespaceAttributeFullName = grammarNs + "." + "NamespaceAttribute";
    const string nameAttributeFullName = grammarNs + "." + "NameAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            (node, _) => IsGrammarType(node),
            (context, _) => GetGrammarType(context)
        ).Collect();

        context.RegisterSourceOutput(provider, Execute);
    }

    private bool IsGrammarType(SyntaxNode node)
    {
        if(node is not (BaseTypeDeclarationSyntax declaration and (InterfaceDeclarationSyntax or EnumDeclarationSyntax)))
        {
            return false;
        }
        // Has [ComplexType] or [SimpleType]
        return declaration.AttributeLists.Any(
            list => list.Attributes.Any(
                attr => GetLocalName(attr.Name) is
                    complexTypeAttributeSimpleName or complexTypeAttributeSimpleName + "Attribute" or
                    simpleTypeAttributeSimpleName or simpleTypeAttributeSimpleName + "Attribute"
            )
        );
    }

    private ITypeSymbol? GetGrammarType(GeneratorSyntaxContext context)
    {
        if(context.Node is not (BaseTypeDeclarationSyntax declaration and (InterfaceDeclarationSyntax or EnumDeclarationSyntax)))
        {
            return null;
        }

        if(context.SemanticModel.GetDeclaredSymbol(declaration) is not ITypeSymbol type)
        {
            return null;
        }

        if(!type.GetAttributes().Any(a => GetQualifiedName(a.AttributeClass) is complexTypeAttributeFullName or simpleTypeAttributeFullName))
        {
            return null;
        }

        return type;
    }

    private void Execute(SourceProductionContext context, ImmutableArray<ITypeSymbol?> types)
    {
        var realTypes = (IEnumerable<ITypeSymbol>)types.Where(t => t != null);

        foreach(var group in realTypes.GroupBy<ITypeSymbol, INamespaceSymbol>(t => t.ContainingNamespace, SymbolEqualityComparer.Default))
        {
            var ns = group.Key;
            var nsName = GetQualifiedName(ns);

            context.AddSource($"{nsName}.Encoder.Generated.cs", GenerateEncoder(ns, group));
            context.AddSource($"{nsName}.Decoder.Generated.cs", GenerateDecoder(ns, group));
            context.AddSource($"{nsName}.NullHandler.Generated.cs", GenerateNullHandler(ns, group));
            context.AddSource($"{nsName}.UniversalHandler.Generated.cs", GenerateUniversalHandler(ns, group));
            context.AddSource($"{nsName}.CapturingHandler.Generated.cs", GenerateCapturingHandler(ns, group));
            context.AddSource($"{nsName}.BaseHandlers.Generated.cs", GenerateBaseHandlers(ns, group));
            context.AddSource($"{nsName}.Extensions.Generated.cs", GenerateExtensions(ns, group));
            context.AddSource($"{nsName}.Tokens.Generated.cs", GenerateTokens(ns, group));
        }
    }

    private static bool UseCustomEncodingForSystemType(ITypeSymbol type)
    {
        var name = GetQualifiedName(type);
        return name == typeof(DateTime).FullName || name == typeof(DateTimeOffset).FullName || name == typeof(Uri).FullName;
    }

    private static void Partition<TElement>(IndentedTextWriter writer, string nameVariable, Action<TElement> handler, IEnumerable<(string name, TElement element)> names)
    {
        if(names.Count() <= 2)
        {
            CheckEach(names);
            return;
        }

        var lengthGroups = names.GroupBy(t => t.name.Length);
        var (minLength, maxLength, visitedLength) = FindGaps(lengthGroups);

        writer.WriteLine($"switch({nameVariable}.Length)");
        writer.WriteLine("{");
        writer.Indent++;

        foreach(var group in lengthGroups)
        {
            int commonLength = group.Key;
            writer.WriteLine($"case {commonLength}:");
            PartitionByDistinguishingCharacter(group, commonLength, 0);
            writer.WriteLine("break;");
        }
        if(visitedLength.Count > 0)
        {
            // Generate empty cases to use CIL switch
            bool any = false;
            while(++minLength < maxLength)
            {
                if(!visitedLength.Contains(minLength))
                {
                    writer.WriteLine($"case {minLength}:");
                    any = true;
                }
            }
            if(any)
            {
                writer.WriteLine("break;");
            }
        }

        writer.Indent--;
        writer.WriteLine("}");

        void PartitionByDistinguishingCharacter(IEnumerable<(string name, TElement element)> names, int commonLength, int checkedCharacters)
        {
            if(checkedCharacters == commonLength)
            {
                // Partitioned enough times to narrow down to the exact case

                if(names.Count() != 1)
                {
                    throw new ApplicationException($"Partitioning by {checkedCharacters} characters did not result in a single case.");
                }

                foreach(var elem in names)
                {
                    handler(elem.element);
                }
                return;
            }

            if(names.Count() <= 2)
            {
                CheckEach(names);
                return;
            }

            // Find an index that produces the most number of groups
            var (index, groups) =
                Enumerable.Range(0, commonLength)
                .Select(i => (i, g: names.GroupBy(t => t.name[i])))
                .OrderByDescending(t => t.g.Count())
                // Minimize number of empty cases
                .ThenBy(t => CountGaps(t.g))
                .First();

            writer.WriteLine($"switch({nameVariable}[{index}])");
            writer.WriteLine("{");
            writer.Indent++;

            foreach(var group in groups)
            {
                writer.WriteLine($"case {FormatLiteral(group.Key)}:");

                // Recurse
                PartitionByDistinguishingCharacter(group, commonLength, checkedCharacters + 1);

                writer.WriteLine("break;");
            }

            var (min, max, visited) = FindGaps(groups);
            if(visited.Count > 0)
            {
                // Generate empty cases
                bool any = false;
                while(++min < max)
                {
                    if(!visited.Contains(min))
                    {
                        writer.WriteLine($"case {FormatLiteral(min)}:");
                        any = true;
                    }
                }
                if(any)
                {
                    writer.WriteLine("break;");
                }
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        void CheckEach(IEnumerable<(string name, TElement element)> names)
        {
            bool firstNameCheck = true;
            foreach(var item in names)
            {
                if(firstNameCheck)
                {
                    firstNameCheck = false;
                }
                else
                {
                    writer.Write("else ");
                }

                writer.WriteLine($"if({nameVariable} == (object){FormatLiteral(item.name)})");
                writer.WriteLine("{");
                writer.Indent++;
                handler(item.element);
                writer.Indent--;
                writer.WriteLine("}");
            }
        }

        static int CountGaps<TValue>(IEnumerable<IGrouping<char, TValue>> groups)
        {
            var (min, max, visited) = FindGaps(groups);
            int count = 0;
            while(++min < max)
            {
                if(!visited.Contains(min))
                {
                    count++;
                }
            }
            return count;
        }

        static (TKey min, TKey max, HashSet<TKey> visited) FindGaps<TKey, TValue>(IEnumerable<IGrouping<TKey, TValue>> groups) where TKey : struct, IComparable<TKey>, IEquatable<TKey>
        {
            TKey min = default, max = default;
            var visited = new HashSet<TKey>();
            foreach(var group in groups)
            {
                var key = group.Key;
                if(!visited.Add(key))
                {
                    continue;
                }

                if(visited.Count <= 1)
                {
                    // First element
                    min = key;
                    max = key;
                    continue;
                }

                if(key.CompareTo(min) < 0)
                {
                    min = key;
                }
                if(key.CompareTo(max) > 0)
                {
                    max = key;
                }
            }
            return (min, max, visited);
        }
    }

    private static void AnalyzeMethod(IMethodSymbol method, out ITypeSymbol? handlerReturnType, out IParameterSymbol? valueParam, out Dictionary<(string localName, string? ns), IParameterSymbol> attributeParams)
    {
        var returnType = method.ReturnType;
        if(returnType is INamedTypeSymbol { IsGenericType: true } namedReturnType && GetQualifiedName(returnType) == typeof(ValueTask).FullName)
        {
            handlerReturnType = namedReturnType.TypeArguments[0];
        }
        else
        {
            handlerReturnType = null;
        }
        attributeParams = new();
        valueParam = null;

        foreach(var param in method.Parameters)
        {
            if(GetName(param) is { } attrName)
            {
                // This parameter is taken from an attribute
                attributeParams.Add(attrName, param);
            }
            else
            {
                // This is the main value
                if(valueParam != null)
                {
                    throw new ApplicationException($"Element method '{method.Name}' has multiple non-attribute parameters.");
                }
                if(handlerReturnType != null)
                {
                    throw new ApplicationException($"Element method '{method.Name}' cannot have both a value parameter and return a handler for the element's contents.");
                }
                valueParam = param;
            }
        }
    }

    private static string? GetNamespace(ITypeSymbol type)
    {
        if(type.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == namespaceAttributeFullName) is { } nsAttr)
        {
            // Default namespace for all elements within
            return nsAttr.ConstructorArguments[0].Value?.ToString();
        }
        return null;
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if(type is INamedTypeSymbol namedType && GetQualifiedName(namedType) == "System.Nullable")
        {
            return namedType.TypeArguments[0];
        }
        return type;
    }

    private static (string localName, string? ns)? GetName(ISymbol symbol)
    {
        if(symbol.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == nameAttributeFullName) is not { } attr)
        {
            return null;
        }

        var nameArgs = attr.ConstructorArguments;

        var localName = nameArgs[0].Value!.ToString();
        string? ns;
        if(nameArgs.Length >= 2 && !nameArgs[1].IsNull)
        {
            ns = nameArgs[1].Value?.ToString();
        }
        else
        {
            ns = null;
        }
        return (localName, ns);
    }

    private static string? Format(ISymbol? symbol)
    {
        return symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string? FormatNullable(ISymbol? symbol)
    {
        return symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));
    }

    private static string? FormatNonGlobal(ISymbol? symbol)
    {
        return symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining));
    }

    public static string? FormatLiteral(char literal)
    {
        return SymbolDisplay.FormatLiteral(literal, true);
    }

    public static string? FormatLiteral(string? literal)
    {
        if(literal == null) return "null";
        return SymbolDisplay.FormatLiteral(literal, true);
    }

    private static string? GetLocalName(NameSyntax name)
    {
        return name switch {
            SimpleNameSyntax simpleName => simpleName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            _ => name.ToString()
        };
    }

    private static string GetQualifiedName(ISymbol? symbol)
    {
        if(symbol == null)
        {
            return "";
        }
        var name = symbol.Name;
        if(symbol.ContainingType is { } containingType)
        {
            return GetQualifiedName(containingType) + "." + name;
        }
        if(symbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace)
        {
            return GetQualifiedName(containingNamespace) + "." + name;
        }
        return name;
    }
}
