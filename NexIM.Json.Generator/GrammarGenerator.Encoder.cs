using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace NexIM.Json.Generator;

partial class GrammarGenerator
{
    private string GenerateEncoder(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using Newtonsoft.Json;");
        writer.WriteLine("using NexIM.Primitives;");
        writer.WriteLine("using NexIM.Primitives.Json;");
        writer.WriteLine("using NexIM.Primitives.Json.Grammar;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Grammar;");
        writer.WriteLine("#nullable disable");
        writer.Write("partial class JsonEncoder : IUniversalHandler");

        // Implement all type encoders
        foreach(var type in types)
        {
            if(type.TypeKind != TypeKind.Enum)
            {
                continue;
            }

            writer.Write($", IValueJsonEncoder<Token<{Format(type)}>>");
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

                // Implement all methods having NameAttribute
                foreach(var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if(GetName(method) is not { } localName)
                    {
                        continue;
                    }

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
                    writer.WriteLine(")");

                    AnalyzeMethod(method, out var handlerReturnType, out var cardinality, out var keyParam, out var valueParam, out var attributeParams);

                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        // Store writer instance for this
                        writer.WriteLine("var writer = this.Writer;");

                        var valueParamType = valueParam != null ? GetUnderlyingType(valueParam.Type) : null;
                        bool isLanguageTaggedString = valueParamType?.Name == "LanguageTaggedString";

                        if(isLanguageTaggedString)
                        {
                            // Treat like an object
                            if(cardinality != 0)
                            {
                                throw new ApplicationException($"Value parameter in method '{method.Name}' is a language-tagged string, but its value kind is not scalar.");
                            }
                            cardinality = 2;
                        }

                        writer.WriteLine($"await this.EnterProperty(writer, {FormatLiteral(localName)}, (ValueKind)({cardinality}));");

                        // Result is object
                        if(handlerReturnType != null || attributeParams.Any())
                        {
                            writer.WriteLine("await writer.WriteStartObjectAsync();");
                        }

                        foreach(var pair in attributeParams)
                        {
                            // Extract attribute if specified
                            var attrLocalName = pair.Key;
                            var param = pair.Value;
                            var paramVar = "v_" + param.Name;

                            var paramType = GetUnderlyingType(param.Type);
                            var typeName = GetQualifiedName(paramType);

                            writer.WriteLine($"if({param.Name} is {{ }} {paramVar})");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine($"await writer.WritePropertyNameAsync({FormatLiteral(attrLocalName)});");
                            if(typeName.StartsWith("System.", System.StringComparison.Ordinal) && !UseCustomEncodingForSystemType(paramType))
                            {
                                writer.Write("await writer.WriteValueAsync(");
                                ParamToString(paramVar, param.Type);
                                writer.WriteLine(");");
                            }
                            else
                            {
                                // Use encoder
                                writer.WriteLine($"await this.Encode<{Format(paramType)}, JsonEncoder>(writer, {paramVar}, this);");
                            }
                            writer.Indent--;
                            writer.WriteLine("}");
                        }

                        if(valueParam != null)
                        {
                            // Write value if specified
                            var paramVar = "v_" + valueParam.Name;

                            var paramType = valueParamType!;
                            var typeName = GetQualifiedName(paramType);

                            writer.WriteLine($"if({valueParam.Name} is {{ }} {paramVar})");
                            writer.WriteLine("{");
                            writer.Indent++;
                            if(typeName.StartsWith("System.") && !UseCustomEncodingForSystemType(paramType))
                            {
                                writer.Write("await writer.WriteValueAsync(");
                                ParamToString(paramVar, valueParam.Type);
                                writer.WriteLine(");");
                            }
                            else if(isLanguageTaggedString)
                            {
                                // Encode as a pair
                                writer.WriteLine($"await this.Encode(writer, ({paramVar}.Language, {paramVar}.Value), this);");
                            }
                            else
                            {
                                // Use encoder
                                writer.WriteLine($"await this.Encode<{Format(paramType)}, JsonEncoder>(writer, {paramVar}, this);");
                            }
                            writer.Indent--;
                            writer.WriteLine("}");
                            if(!isLanguageTaggedString)
                            {
                                writer.WriteLine("else");
                                writer.WriteLine("{");
                                writer.Indent++;
                                writer.WriteLine("await writer.WriteNullAsync();");
                                writer.Indent--;
                                writer.WriteLine("}");
                            }
                        }
                        
                        // Enter contents
                        if(handlerReturnType != null)
                        {
                            writer.WriteLine("return await ForkInner();");
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

                writer.WriteLine($"ValueTask IValueJsonEncoder<{tokenType}>.Encode(JsonWriter writer, {tokenType} token)");
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
            writer.Write(name);
        }
    }
}
