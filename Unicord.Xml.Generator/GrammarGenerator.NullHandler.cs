using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateNullHandler(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using Unicord.Primitives.Xml.Handlers;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Handlers;");
        writer.WriteLine("#nullable disable");
        writer.WriteLine("partial class NullHandler : IUniversalHandler");
        writer.WriteLine("{");
        writer.Indent++;
        {
            // Implement other methods

            writer.WriteLine("ValueTask IPayloadHandler.Other(System.Xml.XmlReader payloadReader) => default;");

            foreach(var type in types)
            {
                if(type.TypeKind != TypeKind.Interface)
                {
                    continue;
                }

                // Implement all methods having NameAttribute
                foreach(var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    // Explicit implementation
                    var returnType = method.ReturnType;
                    writer.Write($"{Format(returnType)} {Format(type)}.{method.Name}(");

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

                    AnalyzeMethod(method, out var handlerReturnType, out _, out _);

                    writer.Write(") => ");

                    // Close or leave opened
                    if(handlerReturnType != null)
                    {
                        writer.WriteLine("new(this);");
                    }
                    else
                    {
                        writer.WriteLine("default;");
                    }
                }
            }
        }
        writer.Indent--;
        writer.WriteLine("}");

        writer.Dispose();
        return sb.ToString();
    }
}
