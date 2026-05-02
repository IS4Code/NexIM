using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace NexIM.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateExtensions(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine("using NexIM.Primitives;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Handlers;");

        writer.WriteLine("#nullable enable");

        writer.WriteLine($"public static class HandlerExtensions");

        writer.WriteLine("{");
        writer.Indent++;

        foreach(var type in types)
        {
            if(type.TypeKind != TypeKind.Interface)
            {
                continue;
            }

            var handlerType = FormatNullable(type);

            var methods = type.GetMembers().OfType<IMethodSymbol>();
            foreach(var method in methods)
            {
                AnalyzeMethod(method, out var handlerReturnType, out var valueParam, out var otherParams);

                var name = method.Name;
                var returnType = FormatNullable(method.ReturnType);

                if(valueParam != null)
                {
                    var paramName = valueParam.Name;
                    var paramType = valueParam.Type;

                    if(otherParams is not { Count: > 0 })
                    {
                        // Single value parameter

                        // List parameter
                        if(paramType.IsValueType)
                        {
                            if(IsNullable(paramType, out var elementType))
                            {
                                // Nullable value type - add overload without
                                writer.WriteLine($"public static async {returnType} {name}Range(this {handlerType} handler, List<{FormatNullable(elementType)}>? {paramName}Range)");
                                WriteListBody();
                            }
                            writer.WriteLine($"public static async {returnType} {name}Range(this {handlerType} handler, List<{FormatNullable(paramType)}>? {paramName}Range)");
                            WriteListBody();
                        }
                        else
                        {
                            // Ignore precise nullability
                            writer.WriteLine($"public static async {returnType} {name}Range(this {handlerType} handler, List<");
                            writer.WriteLine("#nullable disable");
                            writer.WriteLine(Format(paramType));
                            writer.WriteLine("#nullable restore");
                            writer.WriteLine($">? {paramName}Range)");
                            WriteListBody();
                        }

                        void WriteListBody()
                        {
                            writer.WriteLine("{");
                            writer.Indent++;

                            writer.WriteLine($"if({paramName}Range is {{ }} _range)");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine($"foreach(var _item in _range)");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine($"await handler.{method.Name}(_item);");
                            writer.Indent--;
                            writer.WriteLine("}");
                            writer.Indent--;
                            writer.WriteLine("}");

                            writer.Indent--;
                            writer.WriteLine("}");
                        }

                        if(
                            (paramType.IsValueType && IsNullable(paramType, out _)) ||
                            paramType.NullableAnnotation == NullableAnnotation.Annotated
                        )
                        {
                            // Nullable type
                            writer.WriteLine($"public static {returnType} {name}NotNull(this {handlerType} handler, {FormatNullable(paramType)} {paramName})");
                            writer.WriteLine("{");
                            writer.Indent++;

                            writer.WriteLine($"if({paramName} is not {{ }} _val)");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine("return default;");
                            writer.Indent--;
                            writer.WriteLine("}");

                            writer.WriteLine($"return handler.{method.Name}(_val);");

                            writer.Indent--;
                            writer.WriteLine("}");
                        }

                        if((paramType.IsValueType && IsNullable(paramType, out var innerType) ? innerType : paramType).Name == "LanguageTaggedString")
                        {
                            // Localized string
                            writer.WriteLine($"public static async {returnType} {name}LocalizedNotNull(this {handlerType} handler, LocalizedString? {paramName})");
                            writer.WriteLine("{");
                            writer.Indent++;

                            writer.WriteLine($"if({paramName} is not {{ }} _val)");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine("return;");
                            writer.Indent--;
                            writer.WriteLine("}");

                            writer.WriteLine($"foreach(var _str in _val)");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine($"await handler.{method.Name}(_str);");
                            writer.Indent--;
                            writer.WriteLine("}");

                            writer.Indent--;
                            writer.WriteLine("}");
                        }
                    }
                }

                if(handlerReturnType == null)
                {
                    if(method.Parameters.FirstOrDefault(p => (p.Type.IsValueType && IsNullable(p.Type, out var innerType) ? innerType : p.Type).Name == "LanguageTaggedString") is { } localizedParam)
                    {
                        // Localized string parameter
                        var paramName = localizedParam.Name;
                        var paramType = localizedParam.Type;

                        writer.Write($"public static async {returnType} {name}Localized(this {handlerType} handler");
                        foreach(var param in method.Parameters)
                        {
                            writer.Write(", ");
                            if(SymbolEqualityComparer.Default.Equals(param, localizedParam))
                            {
                                writer.Write($"LocalizedString? {paramName}");
                            }
                            else
                            {
                                writer.Write($"{FormatNullable(param.Type)} {param.Name}");
                            }
                        }
                        writer.WriteLine(")");
                        writer.WriteLine("{");
                        writer.Indent++;

                        writer.WriteLine($"if({paramName} is {{ }} _val)");
                        writer.WriteLine("{");
                        writer.Indent++;

                        writer.WriteLine($"foreach(var _str in _val)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        WriteCall("_str");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("else");
                        writer.WriteLine("{");
                        writer.Indent++;

                        WriteCall("null");

                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.Indent--;
                        writer.WriteLine("}");

                        void WriteCall(string textParam)
                        {
                            writer.Write($"await handler.{method.Name}(");
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

                                if(SymbolEqualityComparer.Default.Equals(param, localizedParam))
                                {
                                    writer.Write(textParam);
                                }
                                else
                                {
                                    writer.Write(param.Name);
                                }
                            }
                            writer.WriteLine(");");
                        }
                    }
                }
            }
        }

        writer.Indent--;
        writer.WriteLine("}");

        writer.Dispose();
        return sb.ToString();

        static bool IsNullable(ITypeSymbol type, out ITypeSymbol elementType)
        {
            if(type is INamedTypeSymbol { IsGenericType: true } namedType && GetQualifiedName(namedType) == "System.Nullable")
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }
            elementType = null!;
            return false;
        }
    }
}
