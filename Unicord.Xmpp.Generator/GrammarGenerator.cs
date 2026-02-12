using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        if(node is ClassDeclarationSyntax { Identifier.Text: "XmppDecoder" or "XmppVocabulary" })
        {
            // Include these types because their implementation is generated
            return true;
        }
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
        if(context.Node is not InterfaceDeclarationSyntax declaration)
        {
            return null;
        }

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
        context.AddSource("XmppDecoder.Generated.cs", GenerateDecoder(realTypes!));
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
                var defaultNs = GetNamespace(type);

                // Implement all methods having NameAttribute
                foreach(var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if(GetName(method) is not var (localName, ns))
                    {
                        continue;
                    }

                    ns ??= defaultNs ?? throw new ApplicationException($"Element method '{method.Name}' is missing namespace and no default namespace is configured for the complex type.");

                    // Explicit implementation
                    var returnType = method.ReturnType;
                    sb.Append($"async {Format(returnType)} {Format(type)}.{method.Name}(");

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
                    }

                    AnalyzeMethod(method, out var returnsHandler, out var valueParam, out var attributeParams);

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

    static readonly Regex nonLetterCharacters = new("[^a-zA-Z]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static string GetXmlSimpleName(string text)
    {
        // Also replaces trailing "
        return nonLetterCharacters.Replace(text, " ").Trim();
    }

    private string GenerateDecoder(IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Xml;");
        sb.AppendLine($"namespace {grammarNs};");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("partial class XmppVocabulary");

        var vocabulary = new Dictionary<string, string>();
        var methods = new Dictionary<string, List<IMethodSymbol>>();

        // Cache all vocabulary tokens

        sb.AppendLine("{");
        {
            foreach(var type in types)
            {
                AddKey(GetNamespace(type));
                foreach(var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if(GetName(method) is not var (localName, ns))
                    {
                        continue;
                    }

                    // Remember this method by the local name
                    if(!methods.TryGetValue(localName, out var list))
                    {
                        methods[localName] = list = new();
                    }
                    list.Add(method);

                    AddKey(localName);
                    AddKey(ns);
                    foreach(var param in method.Parameters)
                    {
                        if(GetName(param) is var (attrName, attrNs))
                        {
                            AddKey(attrName);
                            AddKey(attrNs);
                        }
                    }
                }
            }
            void AddKey(string? key)
            {
                if(key == null || vocabulary.ContainsKey(key))
                {
                    return;
                }
                var encoded = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(GetXmlSimpleName(key)).Replace(" ", "");
                vocabulary[key] = encoded;
                sb.AppendLine($"public static readonly Key {encoded} = new({key});");
            }
            sb.AppendLine("private partial void AddKey(string key);");
            sb.AppendLine("private partial void AddKeys()");
            sb.AppendLine("{");
            {
                // Add all cached tokens
                foreach(var encoded in vocabulary.Values)
                {
                    sb.AppendLine($"AddKey({encoded});");
                }
            }
            sb.AppendLine("}");
        }
        sb.AppendLine("}");

        // Generate decoder

        sb.AppendLine("partial class XmppDecoder");
        sb.AppendLine("{");
        {
            sb.AppendLine("private static partial ValueTask<string> ReadElementTextAsync(XmlReader reader);");
            sb.AppendLine("private static partial ValueTask EmptyElementTextAsync(XmlReader reader);");
            sb.AppendLine($"public static partial async ValueTask<Result> DecodePayload(XmlReader reader, {baseNs}.Protocol.IPayloadHandler handler)");
            sb.AppendLine("{");
            {
                // Group by first character to decrease number of checks
                sb.AppendLine("var elementName = reader.Name;");
                sb.AppendLine("var elementNs = reader.NamespaceURI;");
                sb.AppendLine("switch(elementName[0])");
                sb.AppendLine("{");
                {
                    foreach(var charGroup in vocabulary.GroupBy(pair => GetXmlSimpleName(pair.Key)[0]))
                    {
                        sb.AppendLine($"case '{charGroup.Key}':");

                        bool firstNameCheck = true;
                        foreach(var pair in methods)
                        {
                            var elementName = pair.Key;
                            if(GetXmlSimpleName(elementName)[0] != charGroup.Key)
                            {
                                continue;
                            }

                            // Matches element name first character

                            if(firstNameCheck)
                            {
                                firstNameCheck = false;
                            }
                            else
                            {
                                sb.Append("else ");
                            }

                            sb.AppendLine($"if(elementName == XmppVocabulary.{vocabulary[elementName]})");
                            sb.AppendLine("{");
                            {
                                bool firstNamespaceCheck = true;

                                int payloadCounter = 0;

                                foreach(var method in pair.Value)
                                {
                                    if(GetName(method) is not var (_, ns))
                                    {
                                        continue;
                                    }

                                    ns ??= GetNamespace(method.ContainingType) ?? throw new ApplicationException($"Element method '{method.Name}' is missing namespace and no default namespace is configured for the complex type.");

                                    if(firstNamespaceCheck)
                                    {
                                        firstNamespaceCheck = false;
                                    }
                                    else
                                    {
                                        sb.Append("else ");
                                    }

                                    sb.AppendLine($"if(elementNs == XmppVocabulary.{vocabulary[ns]} && handler is {Format(method.ContainingType)} payloadHandler{++payloadCounter})");
                                    sb.AppendLine("{");
                                    {
                                        // Can be handled
                                        AnalyzeMethod(method, out var returnsHandler, out var valueParam, out var attributeParams);
                                        foreach(var pair2 in attributeParams)
                                        {
                                            var (attrName, attrNs) = pair2.Key;
                                            var param = pair2.Value;

                                            // Get value from attribute
                                            sb.Append($"var {param.Name} = reader.GetAttribute(XmppVocabulary.");
                                            sb.Append(vocabulary[attrName]);
                                            if(attrNs != null)
                                            {
                                                sb.Append(", XmppVocabulary.");
                                                sb.Append(vocabulary[attrNs]);
                                            }
                                            sb.Append(") is { } v ? ");
                                            StringToParam(param.Type, "v");
                                            sb.Append(" : ");
                                            DefaultParamValue(param);
                                            sb.AppendLine(";");
                                        }

                                        if(returnsHandler)
                                        {
                                            // Open payload
                                            sb.Append("return new(true, ");
                                            Call();
                                            sb.AppendLine(");");
                                        }
                                        else
                                        {
                                            if(valueParam != null)
                                            {
                                                // Get value from content
                                                sb.Append($"var {valueParam.Name} = (await ReadElementTextAsync(reader)) is {{ }} v ? ");
                                                StringToParam(valueParam.Type, "v");
                                                sb.Append(" : ");
                                                DefaultParamValue(valueParam);
                                                sb.AppendLine(";");
                                            }
                                            else
                                            {
                                                // Expect empty content
                                                sb.AppendLine("await EmptyElementTextAsync(reader);");
                                            }
                                            // Call and return
                                            Call();
                                            sb.AppendLine(";");
                                            sb.AppendLine("return new(true, null);");
                                        }

                                        void Call()
                                        {
                                            sb.Append($"await payloadHandler{payloadCounter}.{method.Name}(");
                                            bool first = true;
                                            foreach(var param in method.Parameters)
                                            {
                                                if(first)
                                                {
                                                    first = false;
                                                }
                                                else
                                                {
                                                    sb.Append(", ");
                                                }
                                                sb.Append(param.Name);
                                            }
                                            sb.Append(')');
                                        }
                                    }
                                    sb.AppendLine("}");
                                }
                            }
                            sb.AppendLine("}");
                        }
                        sb.AppendLine("break;");
                    }
                }
                sb.AppendLine("}");
            }
            sb.AppendLine("return new(false, null);");
            sb.AppendLine("}");
        }
        sb.AppendLine("}");
        return sb.ToString();

        void StringToParam(ITypeSymbol type, string name)
        {
            if(GetQualifiedName(type) != typeof(string).FullName)
            {
                // Needs conversion from string

                if(type is INamedTypeSymbol namedType && GetQualifiedName(namedType) == "System.Nullable")
                {
                    type = namedType.TypeArguments[0];
                }

                sb.Append($"XmlConvert.To{type.Name}({name})");
            }
            else
            {
                sb.Append(name);
            }
        }

        void DefaultParamValue(IParameterSymbol param)
        {
            if(param.HasExplicitDefaultValue && param.ExplicitDefaultValue is { } defaultValue)
            {
                sb.Append($"({Format(param.Type)}){SymbolDisplay.FormatPrimitive(defaultValue, true, false)}");
            }
            else
            {
                sb.Append($"default({Format(param.Type)})");
            }
        }
    }

    private static void AnalyzeMethod(IMethodSymbol method, out bool returnsHandler, out IParameterSymbol? valueParam, out Dictionary<(string localName, string? ns), IParameterSymbol> attributeParams)
    {
        returnsHandler = !IsPlainValueTask(method.ReturnType);
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
                if(returnsHandler)
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
            return nsAttr.ConstructorArguments[0].ToCSharpString();
        }
        return null;
    }

    private static bool IsPlainValueTask(ITypeSymbol type)
    {
        return GetQualifiedName(type) == typeof(ValueTask).FullName && type is not INamedTypeSymbol { IsGenericType: true };
    }

    private static (string localName, string? ns)? GetName(ISymbol symbol)
    {
        if(symbol.GetAttributes().FirstOrDefault(attr => GetQualifiedName(attr.AttributeClass) == nameAttributeFullName) is not { } attr)
        {
            return null;
        }

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
