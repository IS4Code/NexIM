using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unicord.Xmpp.Generator;

[Generator]
public sealed class GrammarGenerator : IIncrementalGenerator
{
    const string baseNs = nameof(Unicord) + "." + nameof(Xmpp);
    const string grammarNs = baseNs + ".Grammar";
    const string complexTypeAttributeSimpleName = "ComplexType";
    const string complexTypeAttributeFullName = grammarNs + "." + complexTypeAttributeSimpleName + "Attribute";
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
        if(node is not InterfaceDeclarationSyntax declaration)
        {
            return false;
        }
        // Has [ComplexType]
        return declaration.AttributeLists.Any(
            list => list.Attributes.Any(
                attr => GetLocalName(attr.Name) is complexTypeAttributeSimpleName or complexTypeAttributeSimpleName + "Attribute"
            )
        );
    }

    private ITypeSymbol? GetGrammarType(GeneratorSyntaxContext context)
    {
        var declaration = (InterfaceDeclarationSyntax)context.Node;

        if(context.SemanticModel.GetDeclaredSymbol(declaration) is not ITypeSymbol type)
        {
            return null;
        }

        if(!type.GetAttributes().Any(a => GetQualifiedName(a.AttributeClass) == complexTypeAttributeFullName))
        {
            return null;
        }

        return type;
    }

    private void Execute(SourceProductionContext context, ImmutableArray<ITypeSymbol?> types)
    {
        var realTypes = types.Where(t => t != null);
        
        context.AddSource("XmppEncoder.Generated.cs", GenerateEncoder(realTypes!));
    }

    private string GenerateEncoder(IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Xml;");
        sb.AppendLine($"namespace {grammarNs};");
        sb.AppendLine("#nullable disable");
        sb.Append("partial class XmppEncoder");

        // Implement all complex type interfaces
        bool firstImplementation = true;
        foreach(var type in types)
        {
            if(firstImplementation)
            {
                firstImplementation = false;
                sb.Append(" : ");
            }
            else
            {
                sb.Append(", ");
            }

            sb.Append(GetQualifiedName(type));
        }

        sb.AppendLine();
        sb.AppendLine("{");
        {
            foreach(var type in types)
            {
                string? defaultNs = null;

                if(type.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == namespaceAttributeFullName) is { } nsAttr)
                {
                    // Default namespace for all elements within
                    defaultNs = nsAttr.ConstructorArguments[0].ToCSharpString();
                }

                // Implement all methods having NameAttribute
                foreach(var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if(method.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == nameAttributeFullName) is not { } elemNameAttr)
                    {
                        continue;
                    }

                    // Get the XML element name this method represents
                    var (localName, ns) = GetName(elemNameAttr);
                    ns ??= defaultNs ?? throw new ApplicationException($"Element method '{method.Name}' is missing namespace and no default namespace is configured for the complex type.");

                    // Explicit implementation
                    var returnType = method.ReturnType;
                    sb.Append($"async {Format(returnType)} {Format(type)}.{method.Name}(");

                    var attributeParams = new Dictionary<(string localName, string? ns), IParameterSymbol>();
                    IParameterSymbol? valueParam = null;

                    bool returnsHandler = GetQualifiedName(returnType) != typeof(ValueTask).FullName || (returnType is INamedTypeSymbol { IsGenericType: true });

                    bool firstParameter = true;
                    foreach(var param in method.Parameters)
                    {
                        if(firstParameter)
                        {
                            firstParameter = false;
                        }
                        else
                        {
                            sb.Append(", ");
                        }

                        sb.Append($"{Format(param.Type)} {param.Name}");

                        if(param.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == nameAttributeFullName) is { } attrNameAttr)
                        {
                            // This parameter is taken from an attribute
                            attributeParams.Add(GetName(attrNameAttr), param);
                        }
                        else
                        {
                            // This is the main value
                            if(valueParam != null)
                            {
                                throw new ApplicationException($"Element method '{method.Name}' has multiple non-attribute parameters.");
                            }
                            if(returnsHandler)
                            {
                                throw new ApplicationException($"Element method '{method.Name}' cannot have both a value parameter and return a handler for the element's contents.");
                            }
                            valueParam = param;
                        }
                    }

                    sb.AppendLine(")");
                    sb.AppendLine("{");
                    {
                        // Store writer instance for this
                        sb.AppendLine("var writer = this.Writer;");

                        // Element start
                        sb.AppendLine($"await writer.WriteStartElementAsync(null, {localName}, {ns});");

                        foreach(var pair in attributeParams)
                        {
                            // Extract attribute if specified
                            var (attrLocalName, attrNs) = pair.Key;
                            var attrParam = pair.Value;
                            var attrName = "v_" + attrParam.Name;
                            sb.AppendLine($"if({attrParam.Name} is {{ }} {attrName})");
                            sb.Append($"await writer.WriteAttributeStringAsync(null, {localName}, {ns}, ");
                            ParamToString(attrName, attrParam.Type);
                            sb.AppendLine(");");
                        }

                        if(valueParam != null)
                        {
                            // Write value if specified
                            var valueName = "v_" + valueParam.Name;
                            sb.AppendLine($"if({valueParam.Name} is {{ }} {valueName})");
                            sb.Append("await writer.WriteStringAsync(");
                            ParamToString(valueName, valueParam.Type);
                            sb.AppendLine(");");
                        }

                        // Close or leave opened
                        if(returnsHandler)
                        {
                            sb.AppendLine("return await ForkInner();");
                        }
                        else
                        {
                            sb.AppendLine("await writer.WriteEndElementAsync();");
                        }
                    }
                    sb.AppendLine("}");
                }
            }
        }
        sb.AppendLine("}");

        return sb.ToString();

        void ParamToString(string name, ITypeSymbol type)
        {
            if(GetQualifiedName(type) != typeof(string).FullName)
            {
                // Needs conversion to string
                sb.Append("XmlConvert.ToString(");
                sb.Append(name);
                sb.Append(')');
            }
            else
            {
                sb.Append(name);
            }
        }
    }

    private static (string localName, string? ns) GetName(AttributeData attr)
    {
        var nameArgs = attr.ConstructorArguments;
        var localName = nameArgs[0].ToCSharpString();
        string? ns;
        if(nameArgs.Length >= 2 && !nameArgs[1].IsNull)
        {
            ns = nameArgs[1].ToCSharpString();
        }
        else
        {
            ns = null;
        }
        return (localName, ns);
    }

    private static string Format(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

    private static string? GetQualifiedName(ISymbol? symbol)
    {
        if(symbol == null)
        {
            return null;
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
