using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unicord.Json.Generator;

[Generator]
public sealed partial class GrammarGenerator : IIncrementalGenerator
{
    const string indent = "    ";

    const string grammarNs = nameof(Unicord) + ".Primitives.Json.Grammar";
    const string complexTypeAttributeSimpleName = "ComplexType";
    const string simpleTypeAttributeSimpleName = "SimpleType";
    const string complexTypeAttributeFullName = grammarNs + "." + complexTypeAttributeSimpleName + "Attribute";
    const string simpleTypeAttributeFullName = grammarNs + "." + simpleTypeAttributeSimpleName + "Attribute";
    const string nameAttributeFullName = grammarNs + "." + "NameAttribute";
    const string keyAttributeFullName = grammarNs + "." + "KeyAttribute";
    const string valueKindAttributeFullName = grammarNs + "." + "ValueKindAttribute";

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

            context.AddSource($"{nsName}.JsonEncoder.Generated.cs", GenerateEncoder(ns, group));
        }
    }

    private static bool UseCustomEncodingForSystemType(ITypeSymbol type)
    {
        var name = GetQualifiedName(type);
        return name == typeof(DateTime).FullName || name == typeof(DateTimeOffset).FullName || name == typeof(Uri).FullName;
    }
    
    private static void AnalyzeMethod(IMethodSymbol method, out ITypeSymbol? handlerReturnType, out int cardinality, out IParameterSymbol? keyParam, out IParameterSymbol? valueParam, out Dictionary<string, IParameterSymbol> attributeParams)
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
        keyParam = null;

        if(method.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == valueKindAttributeFullName) is { } valueKindAttr)
        {
            cardinality = (int)valueKindAttr.ConstructorArguments[0].Value!;
        }
        else
        {
            cardinality = 0;
        }

        foreach(var param in method.Parameters)
        {
            if(GetName(param) is { } attrName)
            {
                // This parameter is taken from an attribute
                attributeParams.Add(attrName, param);
            }
            else if(param.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == keyAttributeFullName) != null)
            {
                if(keyParam != null)
                {
                    throw new ApplicationException($"Member method '{method.Name}' has multiple key parameters.");
                }
                keyParam = param;
            }
            else
            {
                // This is the main value
                if(valueParam != null)
                {
                    throw new ApplicationException($"Member method '{method.Name}' has multiple non-attribute parameters.");
                }
                if(handlerReturnType != null)
                {
                    throw new ApplicationException($"Member method '{method.Name}' cannot have both a value parameter and return a handler for the element's contents.");
                }
                valueParam = param;
            }
        }
    }

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if(type is INamedTypeSymbol namedType && GetQualifiedName(namedType) == "System.Nullable")
        {
            return namedType.TypeArguments[0];
        }
        return type;
    }

    private static string? GetName(ISymbol symbol)
    {
        if(symbol.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == nameAttributeFullName) is not { } attr)
        {
            return null;
        }

        var nameArgs = attr.ConstructorArguments;

        return nameArgs[0].Value!.ToString();
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
        return name switch
        {
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
