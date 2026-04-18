using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateExtensions(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Collections.Generic;");
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
                AnalyzeMethod(method, out _, out var valueParam, out var otherParams);

                if(valueParam != null && otherParams is not { Count: > 0 })
                {
                    // Single value parameter

                    var name = method.Name;
                    var returnType = FormatNullable(method.ReturnType);
                    var paramName = valueParam.Name;
                    var paramType = valueParam.Type;
                    
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

                        writer.WriteLine($"if({paramName}Range is {{ }} range)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine($"foreach(var rangeItem in range)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine($"await handler.{method.Name}(rangeItem);");
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

                        writer.WriteLine($"if({paramName} is not {{ }} val)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("return default;");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine($"return handler.{method.Name}(val);");

                        writer.Indent--;
                        writer.WriteLine("}");
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
