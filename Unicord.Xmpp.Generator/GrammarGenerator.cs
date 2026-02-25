using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unicord.Xmpp.Generator;

[Generator]
public sealed partial class GrammarGenerator : IIncrementalGenerator
{
    const string indent = "    ";

    const string baseNs = nameof(Unicord) + "." + nameof(Xmpp);
    const string grammarNs = baseNs + ".Grammar";
    const string protocolNs = baseNs + ".Protocol";
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

        context.AddSource("XmppEncoder.Generated.cs", GenerateEncoder(realTypes));
        context.AddSource("XmppDecoder.Generated.cs", GenerateDecoder(realTypes));
        context.AddSource("NullHandler.Generated.cs", GenerateNullHandler(realTypes));
        context.AddSource("Tokens.Generated.cs", GenerateTokens(realTypes));
    }

    private string GenerateEncoder(IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using System.Xml;");
        writer.WriteLine("using Unicord.Server.Primitives.Xml;");
        writer.WriteLine($"namespace {grammarNs};");
        writer.WriteLine("#nullable disable");
        writer.Write("partial class XmppEncoder");

        // Implement all interfaces
        bool firstImplementation = true;
        foreach(var type in types)
        {
            if(firstImplementation)
            {
                firstImplementation = false;
                writer.Write(" : ");
            }
            else
            {
                writer.Write(", ");
            }

            switch(type.TypeKind)
            {
                case TypeKind.Interface:
                    writer.Write(Format(type));
                    break;
                case TypeKind.Enum:
                    writer.Write($"IValueXmlEncoder<Token<{Format(type)}>>");
                    break;
            }
        }

        writer.WriteLine();
        writer.WriteLine("{");
        writer.Indent++;
        {
            foreach(var type in types)
            {
                if(type.TypeKind != TypeKind.Interface)
                {
                    continue;
                }

                var defaultNs = GetNamespace(type);

                // Implement all methods having NameAttribute
                foreach(var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if(GetName(method) is not var (localName, ns))
                    {
                        continue;
                    }

                    ns ??= defaultNs;

                    // Explicit implementation
                    var returnType = method.ReturnType;
                    writer.Write($"async {Format(returnType)} {Format(type)}.{method.Name}(");

                    bool firstParameter = true;
                    foreach(var param in method.Parameters)
                    {
                        if(firstParameter)
                        {
                            firstParameter = false;
                        }
                        else
                        {
                            writer.Write(", ");
                        }

                        writer.Write($"{Format(param.Type)} {param.Name}");
                    }

                    AnalyzeMethod(method, out var returnsHandler, out var valueParam, out var attributeParams);

                    writer.WriteLine(")");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        // Store writer instance for this
                        writer.WriteLine("var writer = this.Writer;");

                        // Element start
                        writer.WriteLine($"await writer.WriteStartElementAsync(null, {localName}, {ns ?? "null"});");

                        foreach(var pair in attributeParams)
                        {
                            // Extract attribute if specified
                            var (attrLocalName, attrNs) = pair.Key;
                            var param = pair.Value;
                            var paramVar = "v_" + param.Name;

                            var paramType = GetUnderlyingType(param.Type);
                            var typeName = GetQualifiedName(paramType);

                            writer.WriteLine($"if({param.Name} is {{ }} {paramVar})");
                            writer.WriteLine("{");
                            writer.Indent++;
                            if(typeName.StartsWith("System."))
                            {
                                writer.Write($"await writer.WriteAttributeStringAsync(null, {attrLocalName}, {attrNs ?? "null"}, ");
                                ParamToString(paramVar, param.Type);
                                writer.WriteLine(");");
                            }
                            else
                            {
                                // Use encoder
                                writer.WriteLine($"await this.WriteStartAttributeAsync(writer, null, {attrLocalName}, {attrNs ?? "null"});");
                                writer.WriteLine($"await this.Encode<{Format(paramType)}, XmppEncoder>(writer, {paramVar}, this);");
                                writer.WriteLine($"await this.WriteEndAttributeAsync(writer);");
                            }
                            writer.Indent--;
                            writer.WriteLine("}");
                        }

                        if(valueParam != null)
                        {
                            // Write value if specified
                            var paramVar = "v_" + valueParam.Name;

                            var paramType = GetUnderlyingType(valueParam.Type);
                            var typeName = GetQualifiedName(paramType);

                            writer.WriteLine($"if({valueParam.Name} is {{ }} {paramVar})");
                            writer.WriteLine("{");
                            writer.Indent++;
                            if(typeName.StartsWith("System."))
                            {
                                writer.Write("await writer.WriteStringAsync(");
                                ParamToString(paramVar, valueParam.Type);
                                writer.WriteLine(");");
                            }
                            else
                            {
                                // Use encoder
                                writer.WriteLine($"await this.Encode<{Format(paramType)}, XmppEncoder>(writer, {paramVar}, this);");
                            }
                            writer.Indent--;
                            writer.WriteLine("}");
                        }
                        
                        // Close or leave opened
                        if(returnsHandler)
                        {
                            writer.WriteLine("return await ForkInner();");
                        }
                        else
                        {
                            writer.WriteLine("await writer.WriteEndElementAsync();");
                        }
                    }
                    writer.Indent--;
                    writer.WriteLine("}");
                }
            }

            foreach(var type in types)
            {
                if(type.TypeKind != TypeKind.Enum)
                {
                    continue;
                }

                // Explicit encoder implementation

                var tokenType = $"Token<{Format(type)}>";

                writer.WriteLine($"ValueTask IValueXmlEncoder<{tokenType}>.Encode(XmlWriter writer, {tokenType} token)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"return this.EncodeTokenAsync(writer, token.Value);");
                writer.Indent--;
                writer.WriteLine("}");
            };
        }
        writer.Indent--;
        writer.WriteLine("}");

        writer.Dispose();
        return sb.ToString();

        void ParamToString(string name, ITypeSymbol type)
        {
            if(GetQualifiedName(type) != typeof(string).FullName)
            {
                // Needs conversion to string
                writer.Write("XmlConvert.ToString(");
                writer.Write(name);
                writer.Write(')');
            }
            else
            {
                writer.Write(name);
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
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using System.Xml;");
        writer.WriteLine("using Unicord.Server.Primitives.Xml;");
        writer.WriteLine($"namespace {grammarNs};");
        writer.WriteLine("#nullable disable");
        writer.WriteLine("partial class XmppVocabulary");

        var vocabulary = new Dictionary<string, string>();
        var methods = new Dictionary<string, List<IMethodSymbol>>();

        // Cache all vocabulary tokens

        writer.WriteLine("{");
        writer.Indent++;
        {
            writer.WriteLine("internal static class Generated");
            writer.WriteLine("{");
            writer.Indent++;
            {
                foreach(var type in types)
                {
                    if(type.TypeKind != TypeKind.Interface)
                    {
                        continue;
                    }

                    AddKey(GetNamespace(type), null);
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

                        AddKey(localName, null);
                        AddKey(ns, null);
                        foreach(var param in method.Parameters)
                        {
                            if(GetName(param) is var (attrName, attrNs))
                            {
                                AddKey(attrName, null);
                                AddKey(attrNs, null);
                            }
                        }
                    }
                }
                foreach(var type in types)
                {
                    if(type.TypeKind != TypeKind.Enum)
                    {
                        continue;
                    }

                    foreach(var field in type.GetMembers().OfType<IFieldSymbol>())
                    {
                        if(GetName(field) is not var (localName, ns))
                        {
                            continue;
                        }

                        AddKey(localName, type);
                        AddKey(ns, null);
                    }
                }
                void AddKey(string? key, ITypeSymbol? tokenType)
                {
                    if(key == null || vocabulary.ContainsKey(key))
                    {
                        return;
                    }
                    var encoded = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(GetXmlSimpleName(key)).Replace(" ", "");
                    vocabulary[key] = encoded;
                    var type = $"Token<{Format(tokenType) ?? "Enum"}>";
                    writer.WriteLine($"internal static readonly {type} {encoded} = {type}.FromAtomized({key});");
                }
            }
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine("private partial void AddKey(string key);");
            writer.WriteLine("private partial void AddKeys()");
            writer.WriteLine("{");
            writer.Indent++;
            {
                // Add all cached tokens
                foreach(var encoded in vocabulary.Values)
                {
                    writer.WriteLine($"AddKey(Generated.{encoded});");
                }
            }
            writer.Indent--;
            writer.WriteLine("}");
        }
        writer.Indent--;
        writer.WriteLine("}");

        var names = methods.Select(pair => (key: pair.Key, name: GetXmlSimpleName(pair.Key), list: (IEnumerable<IMethodSymbol>)pair.Value));

        // Generate decoder

        string Key(string? key)
        {
            if(key == null)
            {
                return "\"\"";
            }
            return $"XmppVocabulary.Generated.{vocabulary[key]}";
        }

        writer.Write("partial class XmppDecoder");

        // Implement all token encoders
        bool firstImplementation = true;
        foreach(var type in types)
        {
            if(type.TypeKind != TypeKind.Enum)
            {
                continue;
            }

            if(firstImplementation)
            {
                firstImplementation = false;
                writer.Write(" : ");
            }
            else
            {
                writer.Write(", ");
            }

            writer.Write($"IValueXmlDecoder<Token<{Format(type)}>>");
        }
        writer.WriteLine();

        writer.WriteLine("{");
        writer.Indent++;
        {
            writer.WriteLine($"public partial async ValueTask<Result> DecodePayload(XmlReader reader, {baseNs}.Protocol.IPayloadHandler handler)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                // Group by first character to decrease number of checks
                writer.WriteLine("var elementName = reader.LocalName;");
                writer.WriteLine("var elementNs = reader.NamespaceURI;");

                Switch(names);
            }
            writer.WriteLine("return new(false, null);");
            writer.Indent--;
            writer.WriteLine("}");

            foreach(var type in types)
            {
                if(type.TypeKind != TypeKind.Enum)
                {
                    continue;
                }

                // Explicit decoder implementation

                var tokenType = $"Token<{Format(type)}>";

                writer.WriteLine($"async ValueTask<{tokenType}> IValueXmlDecoder<{tokenType}>.Decode(XmlReader reader)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"return {tokenType}.FromAtomized(await this.DecodeTokenAsync(reader));");
                writer.Indent--;
                writer.WriteLine("}");
            };
        }
        writer.Indent--;
        writer.WriteLine("}");

        void Switch(IEnumerable<(string key, string name, IEnumerable<IMethodSymbol> list)> names)
        {
            Partition(writer, "elementName", list => {
                bool firstNamespaceCheck = true;

                int payloadCounter = 0;

                foreach(var method in list)
                {
                    if(GetName(method) is not var (_, ns))
                    {
                        continue;
                    }

                    // If no namespace, use the type's attribute
                    ns ??= GetNamespace(method.ContainingType);

                    if(firstNamespaceCheck)
                    {
                        firstNamespaceCheck = false;
                    }
                    else
                    {
                        writer.Write("else ");
                    }

                    writer.WriteLine($"if(elementNs == {Key(ns)} && handler is {Format(method.ContainingType)} payloadHandler{++payloadCounter})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        // Can be handled

                        int varCounter = 0;

                        AnalyzeMethod(method, out var returnsHandler, out var valueParam, out var attributeParams);
                        foreach(var pair2 in attributeParams)
                        {
                            var (attrName, attrNs) = pair2.Key;
                            var param = pair2.Value;

                            var paramType = GetUnderlyingType(param.Type);
                            var typeName = GetQualifiedName(paramType);

                            // Get value from attribute

                            UsingIfDisposable(paramType);
                            writer.Write($"var {param.Name} = ");
                            if(typeName == typeof(string).FullName)
                            {
                                // Just use the default as fallback
                                writer.Write($"reader.GetAttribute({Key(attrName)}, {Key(attrNs)}) ?? ");
                            }
                            else if(typeName.StartsWith("System.", StringComparison.Ordinal))
                            {
                                // Standard support
                                var readerMethod = $"ReadContentAs{paramType.Name switch
                                {
                                    // XmlReader names
                                    "Int64" => "Long",
                                    "Single" => "Float",
                                    var n => n
                                }}";
                                if(typeof(XmlReader).GetMethod(readerMethod) != null)
                                {
                                    // Read directly
                                    writer.Write($"reader.MoveToAttribute({Key(attrName)}, {Key(attrNs)}) ? reader.{readerMethod}() : ");
                                }
                                else
                                {
                                    // Through converter
                                    var varName = $"v{++varCounter}";
                                    writer.Write($"reader.GetAttribute({Key(attrName)}, {Key(attrNs)}) is {{ }} {varName} ? XmlConvert.To{paramType.Name}({varName}) : ");
                                }
                            }
                            else
                            {
                                // Go through decoder
                                writer.Write($"reader.MoveToAttribute({Key(attrName)}, {Key(attrNs)}) ? await this.Decode<{Format(paramType)}, XmppDecoder>(reader, this) : ");
                            }
                            DefaultParamValue(param);
                            writer.WriteLine(";");
                        }

                        if(returnsHandler)
                        {
                            // Open payload
                            writer.Write("return new(true, ");
                            Call();
                            writer.WriteLine(");");
                        }
                        else
                        {
                            if(valueParam is { } param)
                            {
                                var paramType = GetUnderlyingType(param.Type);
                                var typeName = GetQualifiedName(paramType);

                                // Get value from content

                                UsingIfDisposable(paramType);
                                writer.Write($"var {param.Name} = await this.OpenElement(reader) ? this.CloseElement(reader, ");
                                if(typeName == typeof(string).FullName)
                                {
                                    writer.Write($"await reader.ReadContentAsStringAsync()");
                                }
                                else if(typeName == typeof(object).FullName)
                                {
                                    writer.Write($"await reader.ReadContentAsObjectAsync()");
                                }
                                else if(typeName.StartsWith("System.", StringComparison.Ordinal))
                                {
                                    // Always through converter
                                    writer.Write($"XmlConvert.To{paramType.Name}(await reader.ReadContentAsStringAsync())");
                                }
                                else
                                {
                                    // Go through decoder
                                    writer.Write($"await this.Decode<{Format(paramType)}, XmppDecoder>(reader, this)");
                                }
                                writer.Write(") : ");
                                DefaultParamValue(param);
                                writer.WriteLine(";");
                            }
                            else
                            {
                                // Expect empty content
                                writer.WriteLine("await this.EmptyElement(reader);");
                            }
                            // Call and return
                            Call();
                            writer.WriteLine(";");
                            writer.WriteLine("return new(true, null);");
                        }

                        void Call()
                        {
                            writer.Write($"await payloadHandler{payloadCounter}.{method.Name}(");
                            bool first = true;
                            foreach(var param in method.Parameters)
                            {
                                if(first)
                                {
                                    first = false;
                                }
                                else
                                {
                                    writer.Write(", ");
                                }
                                writer.Write(param.Name);
                            }
                            writer.Write(')');
                        }
                    }
                    writer.Indent--;
                    writer.WriteLine("}");
                }
            }, names.Select(item => (item.name, item.list)), 0);
        }

        writer.Dispose();
        return sb.ToString();

        void UsingIfDisposable(ITypeSymbol type)
        {
            if(GetQualifiedName(type).StartsWith("Unicord.Server.Primitives.Temporary") || type.Interfaces.Any(i => GetQualifiedName(i) == typeof(IDisposable).FullName))
            {
                writer.Write("using ");
            }
        }

        void DefaultParamValue(IParameterSymbol param)
        {
            if(param.HasExplicitDefaultValue && param.ExplicitDefaultValue is { } defaultValue)
            {
                writer.Write($"({Format(param.Type)}){SymbolDisplay.FormatPrimitive(defaultValue, true, false)}");
            }
            else
            {
                writer.Write($"default({Format(param.Type)})");
            }
        }
    }
    
    private static void Partition<TElement>(IndentedTextWriter writer, string nameVariable, Action<TElement> handler, IEnumerable<(string name, TElement element)> names, int pos)
    {
        writer.WriteLine($"switch({nameVariable}[{pos}])");
        writer.WriteLine("{");
        writer.Indent++;
        {
            foreach(var group in names.GroupBy(t => t.name[pos]))
            {
                writer.WriteLine($"case {SymbolDisplay.FormatLiteral(group.Key, true)}:");

                // Number of characters needed to do another partitioning
                int minLongerLength = pos + 2;

                // Names that would (not) be partitioned
                var longer = group.Where(p => p.name.Length >= minLongerLength);
                var shorter = group.Where(p => p.name.Length < minLongerLength);

                const int minCountToNestedSwitch = 5;
                if(longer.Take(minCountToNestedSwitch).Count() >= minCountToNestedSwitch)
                {
                    // Too many checks, partition again

                    // Find the prefix from which differences start to occur
                    var sample = longer.First().name;
                    var differenceFrom = Enumerable.Range(minLongerLength, sample.Length - minLongerLength + 1).Where(length => {
                        var prefix = sample.Substring(0, length);
                        // Not a common prefix
                        return !longer.All(t => t.name.StartsWith(prefix, StringComparison.Ordinal));
                    }).Select(l => (int?)l).FirstOrDefault() ?? (sample.Length - 1);

                    if(differenceFrom > minCountToNestedSwitch)
                    {
                        // Partition only those that differ after the common prefix

                        longer = group.Where(p => p.name.Length >= differenceFrom);
                        shorter = group.Where(p => p.name.Length < differenceFrom);
                    }

                    writer.WriteLine($"if({nameVariable}.Length >= {differenceFrom})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    Partition(writer, nameVariable, handler, longer, differenceFrom - 1);
                    writer.Indent--;
                    writer.WriteLine("}");

                    if(shorter.Any())
                    {
                        // Check the rest
                        writer.WriteLine("else");
                    }
                }
                else
                {
                    shorter = group;
                }

                bool firstNameCheck = true;
                foreach(var item in shorter)
                {
                    // Matches element name first character

                    if(firstNameCheck)
                    {
                        firstNameCheck = false;
                    }
                    else
                    {
                        writer.Write("else ");
                    }

                    writer.WriteLine($"if({nameVariable} == (object){SymbolDisplay.FormatLiteral(item.name, true)})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    handler(item.element);
                    writer.Indent--;
                    writer.WriteLine("}");
                }
                writer.WriteLine("break;");
            }
        }
        writer.Indent--;
        writer.WriteLine("}");
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

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if(type is INamedTypeSymbol namedType && GetQualifiedName(namedType) == "System.Nullable")
        {
            return namedType.TypeArguments[0];
        }
        return type;
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

    private static string? Format(ISymbol? symbol)
    {
        return symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
