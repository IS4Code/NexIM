using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Unicord.Xml.Generator;

partial class GrammarGenerator
{
    private string GenerateBaseHandlers(INamespaceSymbol container, IEnumerable<ITypeSymbol> types)
    {
        var sb = new StringBuilder();
        var writer = new IndentedTextWriter(new StringWriter(sb), indent);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine($"namespace {FormatNonGlobal(container)}.Handlers;");
        writer.WriteLine("#nullable disable");

        foreach(var type in types)
        {
            if(type.TypeKind != TypeKind.Interface)
            {
                continue;
            }

            // Check all implemented interfaces in the same namespace
            var interfaces = type.Interfaces.Where(t => t.ContainingNamespace.Equals(container, SymbolEqualityComparer.Default)).ToList();
            if(interfaces.Count == 0)
            {
                // Base class is implemented manually
                continue;
            }

            var name = type.Name.Substring(1);
            writer.Write($"public abstract class {name}<TContext> : ");

            // The first interface becomes the base class
            var primaryInterface = interfaces[0];
            interfaces.RemoveAt(0);
            var primaryInterfaceName = primaryInterface.Name.Substring(1);
            writer.Write($"{primaryInterfaceName}<TContext>, ");
            foreach(var interfaceType in interfaces)
            {
                // Implement remaining interfaces
                writer.Write($"{Format(interfaceType)}, ");
            }
            // And the current interface
            writer.WriteLine($"{Format(type)} where TContext : IPayloadHandlerContext");

            writer.WriteLine("{");
            writer.Indent++;

            // Implement all methods having NameAttribute
            var methods = type.GetMembers().Concat(interfaces.SelectMany(i => i.GetMembers())).OfType<IMethodSymbol>();
            foreach(var method in methods)
            {
                AnalyzeMethod(method, out var handlerReturnType, out _, out _);

                // Generate a handler method that to indicate success
                writer.WriteLineNoTabs("#nullable enable");
                writer.Write($"protected virtual {(handlerReturnType != null ? $"ValueTask<{FormatNullable(handlerReturnType.WithNullableAnnotation(NullableAnnotation.Annotated))}>" : "ValueTask<bool>")} On{method.Name}(");
                WriteParameters(method, FormatNullable);
                writer.WriteLine(")");
                writer.WriteLine("{");
                writer.Indent++;
                // Returns false or null
                writer.WriteLine("return default;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLineNoTabs("#nullable disable");

                // Explicit implementation
                writer.Write($"async {Format(method.ReturnType)} {Format(method.ContainingType)}.{method.Name}(");
                WriteParameters(method, Format);
                writer.WriteLine(")");

                writer.WriteLine("{");
                writer.Indent++;
                
                writer.WriteLine("bool _exit = await this.OnEnter();");
                writer.WriteLine("try");
                writer.WriteLine("{");
                writer.Indent++;

                if(handlerReturnType == null)
                {
                    // No return type
                    writer.Write($"if(await this.On{method.Name}(");
                    WriteArguments(method);
                    writer.WriteLine(") || this.Decoding)");
                    writer.WriteLine("{");
                    writer.Indent++;

                    // Either handled or should be ignored
                    writer.WriteLine("return;");

                    writer.Indent--;
                    writer.WriteLine("}");

                    // Copy the instruction to Other
                    writer.WriteLine("await using var _encoder = this.GetEncoder(false);");
                    writer.WriteLine($"{Format(type)} _impl = _encoder;");
                    writer.Write($"await _impl.{method.Name}(");
                    WriteArguments(method);
                    writer.WriteLine(");");
                }
                else
                {
                    writer.Write($"if(await this.On{method.Name}(");
                    WriteArguments(method);
                    writer.WriteLine(") is { } _handler)");
                    writer.WriteLine("{");
                    writer.Indent++;

                    // Handler produced
                    writer.WriteLine("if(_exit)");
                    writer.WriteLine("{");
                    writer.Indent++;

                    writer.WriteLine("_exit = false;");
                    writer.WriteLine($"return new Delegating{handlerReturnType.Name.Substring(1)}<ExitDisposable>(_handler, new ExitDisposable(this));");

                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine("return _handler;");

                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine("if(this.Decoding)");
                    writer.WriteLine("{");
                    writer.Indent++;

                    // Should be ignored
                    writer.WriteLine("return NullHandler.Instance;");

                    writer.Indent--;
                    writer.WriteLine("}");

                    // Return a handler that copies the contents to Other (encoder must not be disposed)
                    writer.WriteLine($"{Format(type)} _encoder = this.GetEncoder(_exit);");
                    writer.Write($"_handler = await _encoder.{method.Name}(");
                    WriteArguments(method);
                    writer.WriteLine(");");
                    writer.WriteLine("_exit = false;");
                    writer.WriteLine("return _handler;");
                }

                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("finally");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("if(_exit)");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("await this.OnExit();");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");

            // Require all methods to be overridden
            writer.WriteLine($"public abstract class Base{name}<TContext> : {name}<TContext> where TContext : IPayloadHandlerContext");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLineNoTabs("#nullable enable");
            foreach(var method in methods)
            {
                AnalyzeMethod(method, out var handlerReturnType, out _, out _);

                // Generate a handler method that to indicate success
                writer.Write($"protected abstract override {(handlerReturnType != null ? $"ValueTask<{FormatNullable(handlerReturnType.WithNullableAnnotation(NullableAnnotation.Annotated))}>" : "ValueTask<bool>")} On{method.Name}(");
                WriteParameters(method, FormatNullable);
                writer.WriteLine(");");
            }
            writer.WriteLineNoTabs("#nullable disable");

            writer.Indent--;
            writer.WriteLine("}");

            // Delegate to another handler
            writer.Write($"public class Delegating{name}<TDisposable>({Format(type)} _inner, TDisposable _disposable) : Delegating{primaryInterfaceName}<TDisposable>(_inner, _disposable), ");
            foreach(var interfaceType in interfaces)
            {
                // Implement remaining interfaces
                writer.Write($"{Format(interfaceType)}, ");
            }
            // And the current interface
            writer.WriteLine($"{Format(type)} where TDisposable : IAsyncDisposable");

            writer.WriteLine("{");
            writer.Indent++;

            foreach(var method in methods)
            {
                AnalyzeMethod(method, out var handlerReturnType, out _, out _);

                // Explicit implementation
                writer.Write($"{Format(method.ReturnType)} {Format(method.ContainingType)}.{method.Name}(");
                WriteParameters(method, Format);
                writer.WriteLine(")");

                writer.WriteLine("{");
                writer.Indent++;

                // Call inner handler
                writer.Write($"return _inner.{method.Name}(");
                WriteArguments(method);
                writer.WriteLine(");");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");

            void WriteParameters(IMethodSymbol method, Func<ISymbol?, string?> format)
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

                    writer.Write($"{format(param.Type)} {param.Name}");
                }
            }

            void WriteArguments(IMethodSymbol method)
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

        writer.Dispose();
        return sb.ToString();
    }
}
