using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xmpp.Generator;

partial class GrammarGenerator
{
    private string GenerateNullHandler(IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), "    ");

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using System.Xml;");
        writer.WriteLine($"namespace {protocolNs};");
        writer.WriteLine("#nullable disable");
        writer.Write("partial class NullHandler : IPayloadHandler, IStreamHandler");

        // Implement all complex type interfaces
        foreach(var type in types)
        {
            writer.Write(", ");
            writer.Write(GetQualifiedName(type));
        }

        writer.WriteLine();
        writer.WriteLine("{");
        writer.Indent++;
        {
            // Implement other methods

            writer.WriteLine("ValueTask<IInfoQueryHandler> IStreamHandler.InfoQuery(in Stanza stanza) => new(this);");
            writer.WriteLine("ValueTask<IMessageHandler> IStreamHandler.Message(in Stanza stanza) => new(this);");
            writer.WriteLine("ValueTask<IPresenceHandler> IStreamHandler.Presence(in Stanza stanza) => new(this);");
            writer.WriteLine("ValueTask IPayloadHandler.Other(System.Xml.Linq.XElement payload) => default;");

            foreach(var type in types)
            {
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

                    AnalyzeMethod(method, out var returnsHandler, out var valueParam, out var attributeParams);

                    writer.Write(") => ");

                    // Close or leave opened
                    if(returnsHandler)
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
