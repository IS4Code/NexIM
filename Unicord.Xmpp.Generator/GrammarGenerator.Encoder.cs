using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xmpp.Generator;

partial class GrammarGenerator
{
    private string GenerateEncoder(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using System.Xml;");
        writer.WriteLine("using Unicord.Server.Primitives.Xml;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Grammar;");
        writer.WriteLine("#nullable disable");
        writer.Write("partial class Encoder");

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
                        writer.WriteLine($"await writer.WriteStartElementAsync(null, {FormatLiteral(localName)}, {FormatLiteral(ns)});");

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
                                writer.Write($"await writer.WriteAttributeStringAsync(null, {FormatLiteral(attrLocalName)}, {FormatLiteral(attrNs)}, ");
                                ParamToString(paramVar, param.Type);
                                writer.WriteLine(");");
                            }
                            else
                            {
                                // Use encoder
                                writer.WriteLine($"await this.WriteStartAttributeAsync(writer, null, {FormatLiteral(attrLocalName)}, {FormatLiteral(attrNs)});");
                                writer.WriteLine($"await this.Encode<{Format(paramType)}, Encoder>(writer, {paramVar}, this);");
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
                                writer.WriteLine($"await this.Encode<{Format(paramType)}, Encoder>(writer, {paramVar}, this);");
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
}
