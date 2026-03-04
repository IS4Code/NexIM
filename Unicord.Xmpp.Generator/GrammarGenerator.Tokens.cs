using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xmpp.Generator;

partial class GrammarGenerator
{
    private string GenerateTokens(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.ComponentModel;");
        writer.WriteLine("using Unicord.Server.Primitives.Xml;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)};");
        writer.WriteLine("#nullable disable");

        writer.WriteLine("public static class Extensions");
        writer.WriteLine("{");
        writer.Indent++;
        {
            foreach(var type in types)
            {
                if(type.TypeKind != TypeKind.Enum)
                {
                    continue;
                }

                var typeName = Format(type);

                var list = new List<(string key, string field)>();

                writer.WriteLine($"public static Token<{typeName}> ToToken(this {typeName} value)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"return Token<{typeName}>.FromAtomized(value switch {{");
                writer.Indent++;
                foreach(var field in type.GetMembers().OfType<IFieldSymbol>())
                {
                    if(GetName(field) is not var (name, ns))
                    {
                        continue;
                    }

                    if(ns != null)
                    {
                        throw new ApplicationException("Namespaces in simple types are not supported.");
                    }

                    var fieldName = Format(field)!;

                    // Case label
                    writer.WriteLine($"{typeName}.{fieldName} => {FormatLiteral(name)},");

                    list.Add((name, fieldName));
                }
                writer.WriteLine($"_ => throw new InvalidEnumArgumentException(null, (int)value, typeof({typeName}))");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");

                writer.WriteLine($"public static {typeName}? ToEnum(this Token<{typeName}> token)");
                writer.WriteLine("{");
                writer.Indent++;
                {
                    writer.WriteLine("var name = token.Value;");
                    Partition(writer, "name", fieldName => {
                        writer.WriteLine($"return {typeName}.{fieldName};");
                    }, list);
                    writer.WriteLine($"return ({typeName})(-1);");
                }
                writer.Indent--;
                writer.WriteLine("}");
            }
        }
        writer.Indent--;
        writer.WriteLine("}");

        writer.Dispose();
        return sb.ToString();
    }
}

