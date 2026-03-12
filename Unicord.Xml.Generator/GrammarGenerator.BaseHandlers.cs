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
        writer.WriteLine("using System.Xml;");
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
                writer.Write($"protected virtual {FormatNullable(method.ReturnType)} On{method.Name}(");
                WriteParameters(method, FormatNullable);
                writer.WriteLine(")");
                writer.WriteLine("{");
                writer.Indent++;

                // Return a sentinel task
                if(handlerReturnType != null)
                {
                    writer.WriteLine($"return DefaultImplementation<{FormatNullable(handlerReturnType)}>.ValueTask;");
                }
                else
                {
                    writer.WriteLine("return DefaultImplementation.ValueTask;");
                }

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

                // Call the implementation
                writer.Write($"var _task = this.On{method.Name}(");
                WriteArguments(method);
                writer.WriteLine(");");

                if(handlerReturnType == null)
                {
                    // No return type

                    // Use the implementation if provided
                    writer.WriteLine("if(!_task.Equals(DefaultImplementation.ValueTask)) { await _task; return; }");
                    
                    // Ignore if called from Other
                    writer.WriteLine("if(this.Decoding) return;");

                    // Copy the instruction to Other
                    writer.WriteLine("await using var _encoder = this.GetEncoder(false);");
                    writer.WriteLine($"{Format(type)} _impl = _encoder;");
                    writer.Write($"await _impl.{method.Name}(");
                    WriteArguments(method);
                    writer.WriteLine(");");
                }
                else
                {
                    // Handler expected
                    writer.WriteLine($"{Format(handlerReturnType)} _handler;");

                    writer.WriteLine($"if(!_task.Equals(DefaultImplementation<{Format(handlerReturnType)}>.ValueTask))");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        // Use the implementation
                        writer.WriteLine("_handler = await _task;");

                        writer.WriteLine("if(_exit)");
                        writer.WriteLine("{");
                        writer.Indent++;

                        // Wrap return handler
                        writer.WriteLine("_exit = false;");
                        writer.WriteLine($"return new Delegating{handlerReturnType.Name.Substring(1)}<{Format(handlerReturnType)}, ExitDisposable, EmptyPayloadHandlerContext>(_handler, new ExitDisposable(this));");

                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("return _handler;");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    // Ignore if called from Other
                    writer.WriteLine("if(this.Decoding) return NullHandler.Instance;");

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
                // Exit if requested
                writer.WriteLine("if(_exit) await this.OnExit();");
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
                // Remove default implementation
                writer.Write($"protected abstract override {FormatNullable(method.ReturnType)} On{method.Name}(");
                WriteParameters(method, FormatNullable);
                writer.WriteLine(");");
            }
            writer.WriteLineNoTabs("#nullable disable");

            writer.Indent--;
            writer.WriteLine("}");

            // Delegate to another handler
            writer.WriteLine($"public abstract class BaseDelegating{name}<THandler, TDisposable, TContext> : Base{name}<TContext> where THandler : {Format(type)} where TDisposable : IAsyncDisposable where TContext : IPayloadHandlerContext");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLineNoTabs("#nullable enable");
            writer.WriteLine("protected abstract THandler InnerHandler { get; }");
            writer.WriteLine("protected abstract TDisposable Disposable { get; }");
            foreach(var method in methods)
            {
                // Explicit implementation
                writer.Write($"protected override {FormatNullable(method.ReturnType)} On{method.Name}(");
                WriteParameters(method, FormatNullable);
                writer.WriteLine(")");

                writer.WriteLine("{");
                writer.Indent++;

                // Return from inner
                writer.Write($"return this.InnerHandler.{method.Name}(");
                WriteArguments(method);
                writer.WriteLine(");");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.WriteLine("protected override ValueTask OnOther(XmlReader payloadReader)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("return this.InnerHandler.Other(payloadReader);");
            }
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine("protected override ValueTask OnUnrecognized(XmlReader payloadReader) => default;");

            writer.WriteLine("public async override ValueTask DisposeAsync()");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("try");
                writer.WriteLine("{");
                writer.Indent++;
                {
                    writer.WriteLine("await this.InnerHandler.DisposeAsync();");
                }
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("finally");
                writer.WriteLine("{");
                writer.Indent++;
                {
                    writer.WriteLine("await this.Disposable.DisposeAsync();");
                }
                writer.Indent--;
                writer.WriteLine("}");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLineNoTabs("#nullable disable");

            writer.Indent--;
            writer.WriteLine("}");

            // Pass in the constructor
            writer.WriteLine($"public class Delegating{name}<THandler, TDisposable, TContext>(THandler handler, TDisposable disposable) : BaseDelegating{name}<THandler, TDisposable, TContext> where THandler : {Format(type)} where TDisposable : IAsyncDisposable where TContext : IPayloadHandlerContext");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLineNoTabs("#nullable enable");
            writer.WriteLine("protected sealed override THandler InnerHandler => handler;");
            writer.WriteLine("protected sealed override TDisposable Disposable => disposable;");
            writer.WriteLineNoTabs("#nullable disable");

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
