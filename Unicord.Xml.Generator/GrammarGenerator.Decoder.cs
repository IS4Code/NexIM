using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NexIM.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateDecoder(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using System.Xml;");
        writer.WriteLine("using NexIM.Primitives;");
        writer.WriteLine("using NexIM.Primitives.Xml;");
        writer.WriteLine("using NexIM.Primitives.Xml.Handlers;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Grammar;");
        writer.WriteLine("#nullable disable");
        writer.WriteLine("partial class Vocabulary");

        var vocabulary = new HashSet<string>();
        var methods = new Dictionary<string, List<IMethodSymbol>>();

        // Cache all vocabulary tokens

        writer.WriteLine("{");
        writer.Indent++;
        {
            foreach(var type in types)
            {
                if(type.TypeKind != TypeKind.Interface)
                {
                    continue;
                }

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

                    AddKey(localName);
                    AddKey(ns);
                }
            }
            void AddKey(string? key)
            {
                if(key != null)
                {
                    vocabulary.Add(key);
                }
            }

            writer.WriteLine("private partial void AddKey(string key);");
            writer.WriteLine("private partial void AddKeys()");
            writer.WriteLine("{");
            writer.Indent++;
            {
                // Add all cached tokens
                foreach(var name in vocabulary)
                {
                    writer.WriteLine($"AddKey({FormatLiteral(name)});");
                }
            }
            writer.Indent--;
            writer.WriteLine("}");
        }
        writer.Indent--;
        writer.WriteLine("}");

        var names = methods.Select(pair => (name: pair.Key, list: (IEnumerable<IMethodSymbol>)pair.Value));

        // Generate decoder

        writer.Write("partial class Decoder");

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
            writer.WriteLine($"public partial async ValueTask<Result> DecodePayload(XmlReader reader, IPayloadHandler handler)");
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

        void Switch(IEnumerable<(string name, IEnumerable<IMethodSymbol> list)> names)
        {
            Partition(writer, "elementName", list => {
                const string defaultNs = "\0";

                var methods = list
                    // Find methods with a namespace
                    .Select(m => (m, n: GetName(m)))
                    .Where(t => t.n.HasValue)
                    .Select(t => (t.m, ns: t.n.GetValueOrDefault().ns ?? GetNamespace(t.m.ContainingType) ?? defaultNs))
                    // Group by namespace
                    .GroupBy(t => t.ns, t => t.m)
                    .ToDictionary(g => g.Key);

                bool hasDefault = methods.TryGetValue(defaultNs, out var defaultMethod);

                if(hasDefault)
                {
                    // Check default namespace first
                    methods.Remove(defaultNs);

                    writer.WriteLine($"if(elementNs == (object)this.GetDefaultNamespace(reader.NameTable))");
                    writer.WriteLine("{");
                    writer.Indent++;
                    Decode(defaultMethod);
                    writer.Indent--;
                    writer.WriteLine("}");

                    if(methods.Count == 0)
                    {
                        // Generate no further code
                        hasDefault = false;
                    }
                    else
                    {
                        writer.WriteLine("else");
                        writer.WriteLine("{");
                        writer.Indent++;
                    }
                }

                Partition(writer, "elementNs", Decode, methods.Select(p => (p.Key, p.Value)));

                if(hasDefault)
                {
                    writer.Indent--;
                    writer.WriteLine("}");
                }

                void Decode(IEnumerable<IMethodSymbol> methods)
                {
                    int payloadCounter = 0;

                    foreach(var method in methods)
                    {
                        writer.WriteLine($"if(handler is {Format(method.ContainingType)} payloadHandler{++payloadCounter})");
                        writer.WriteLine("{");
                        writer.Indent++;
                        {
                            // Can be handled

                            int varCounter = 0;

                            AnalyzeMethod(method, out var handlerReturnType, out var valueParam, out var attributeParams);

                            bool onAttribute = false;
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
                                    writer.Write($"reader.GetAttribute({FormatLiteral(attrName)}, {FormatLiteral(attrNs)}) ?? ");
                                }
                                else if(typeName.StartsWith("System.", StringComparison.Ordinal) && !UseCustomEncodingForSystemType(paramType))
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
                                        writer.Write($"reader.MoveToAttribute({FormatLiteral(attrName)}, {FormatLiteral(attrNs)}) ? reader.{readerMethod}() : ");
                                        onAttribute = true;
                                    }
                                    else
                                    {
                                        // Through converter
                                        var varName = $"v{++varCounter}";
                                        writer.Write($"reader.GetAttribute({FormatLiteral(attrName)}, {FormatLiteral(attrNs)}) is {{ }} {varName} ? XmlConvert.To{paramType.Name}({varName}) : ");
                                    }
                                }
                                else
                                {
                                    // Go through decoder
                                    writer.Write($"reader.MoveToAttribute({FormatLiteral(attrName)}, {FormatLiteral(attrNs)}) ? await this.Decode<{Format(paramType)}, Decoder>(reader, this) : ");
                                    onAttribute = true;
                                }
                                DefaultParamValue(param);
                                writer.WriteLine(";");
                            }

                            if(onAttribute)
                            {
                                // Move back
                                writer.WriteLine("reader.MoveToElement();");
                            }

                            if(handlerReturnType != null)
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
                                    else if(typeName.StartsWith("System.", StringComparison.Ordinal) && !UseCustomEncodingForSystemType(paramType))
                                    {
                                        // Always through converter
                                        writer.Write($"XmlConvert.To{paramType.Name}(await reader.ReadContentAsStringAsync())");
                                    }
                                    else
                                    {
                                        // Go through decoder
                                        writer.Write($"await this.Decode<{Format(paramType)}, Decoder>(reader, this)");
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
                }
            }, names);
        }

        writer.Dispose();
        return sb.ToString();

        void UsingIfDisposable(ITypeSymbol type)
        {
            if(GetQualifiedName(type).StartsWith("NexIM.Primitives.Temporary") || type.Interfaces.Any(i => GetQualifiedName(i) == typeof(IDisposable).FullName))
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
}
