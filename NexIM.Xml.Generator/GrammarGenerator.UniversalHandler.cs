using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace NexIM.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateUniversalHandler(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine($"namespace {FormatNonGlobal(container)};");
        writer.WriteLine("#nullable disable");
        writer.Write("partial interface IUniversalHandler");

        // Implement all handler interfaces
        bool firstImplementation = true;
        foreach(var type in types.Concat(types.SelectMany(t => t.Interfaces)).Distinct<ITypeSymbol>(SymbolEqualityComparer.Default))
        {
            if(type.TypeKind != TypeKind.Interface)
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
            writer.Write(Format(type));
        }

        writer.WriteLine();
        writer.WriteLine("{");
        writer.WriteLine("}");

        writer.Dispose();
        return sb.ToString();
    }
}
