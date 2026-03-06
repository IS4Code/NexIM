using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateCapturingHandler(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Handlers;");
        writer.WriteLine("#nullable disable");
        writer.Write("partial class CapturingHandler<THandler> : IPayloadHandler, IStreamHandler");

        // Implement all interfaces
        foreach(var type in types)
        {
            if(type.TypeKind != TypeKind.Interface)
            {
                continue;
            }

            writer.Write(", ");
            writer.Write(GetQualifiedName(type));
        }

        writer.WriteLine();
        writer.WriteLine("{");
        writer.Indent++;
        {
            // Implement other methods

            StanzaHandler("IInfoQueryHandler", "InfoQuery");
            StanzaHandler("IMessageHandler", "Message");
            StanzaHandler("IPresenceHandler", "Presence");

            void StanzaHandler(string type, string name)
            {
                writer.WriteLine($"ValueTask<{type}> IStreamHandler.{name}(in Stanza stanza)");

                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("var copy = stanza;");
                writer.WriteLine($"var inner = this.ForkInner<{type}>();");
                writer.WriteLine("this.Capture<IStreamHandler>(async h => {");
                writer.Indent++;

                writer.WriteLine($"await using var handler = await h.{name}(copy);");
                writer.WriteLine("await inner.Replay(handler);");

                writer.Indent--;
                writer.WriteLine("});");
                writer.WriteLine("return new(inner);");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.WriteLine("ValueTask IPayloadHandler.Other(System.Xml.Linq.XElement payload)");

            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("this.Capture<IPayloadHandler>(h => h.Other(payload));");
            writer.WriteLine("return default;");

            writer.Indent--;
            writer.WriteLine("}");

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

                    writer.WriteLine(")");

                    AnalyzeMethod(method, out var handlerReturnType, out _, out _);

                    writer.WriteLine("{");
                    writer.Indent++;
                    if(handlerReturnType == null)
                    {
                        // Just capture the call and arguments
                        writer.Write($"this.Capture<{Format(type)}>(h => h.{method.Name}(");
                        WriteArguments();
                        writer.WriteLine("));");
                        writer.WriteLine("return default;");
                    }
                    else
                    {
                        // Open a new handler
                        writer.WriteLine($"var inner = this.ForkInner<{Format(handlerReturnType)}>();");
                        writer.WriteLine($"this.Capture<{Format(type)}>(async h => {{");
                        writer.Indent++;

                        writer.Write($"await using var handler = await h.{method.Name}(");
                        WriteArguments();
                        writer.WriteLine(");");
                        writer.WriteLine("await inner.Replay(handler);");

                        writer.Indent--;
                        writer.WriteLine("});");
                        writer.WriteLine("return new(inner);");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    void WriteArguments()
                    {
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

                            writer.Write(param.Name);
                        }
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
