using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace NexIM.Primitives;

public readonly record struct Hex<TValue>(TValue Value) : IDisposable where TValue : IReadOnlyList<byte>
{
    public static implicit operator TValue(Hex<TValue> obj) => obj.Value;
    public static implicit operator Hex<TValue>(TValue value) => new(value);

    public override string ToString()
    {
        var value = Value;
        var sb = new StringBuilder(value.Count * 2);
        foreach(var b in value)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    void IDisposable.Dispose()
    {
        (Value as IDisposable)?.Dispose();
    }
}

public readonly record struct Base64<TValue>(TValue Value) : IDisposable where TValue : IReadOnlyList<byte>
{
    public static implicit operator TValue(Base64<TValue> obj) => obj.Value;
    public static implicit operator Base64<TValue>(TValue value) => new(value);

    static readonly XmlWriterSettings writerSettings = new() {
        Async = false,
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = false,
        NewLineHandling = NewLineHandling.Entitize,
        OmitXmlDeclaration = true,
        CheckCharacters = false
    };

    public override string ToString()
    {
        var value = Value;
        var sb = new StringBuilder((value.Count + 2) / 3 * 4);

        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(1);
        try
        {
            using var writer = new StringWriter(sb);
            using var xml = XmlWriter.Create(writer, writerSettings);
            foreach(var b in value)
            {
                buffer[0] = b;
                xml.WriteBase64(buffer, 0, 1);
            }
        }
        finally
        {
            buffer[0] = 0;
            pool.Return(buffer);
        }

        return sb.ToString();
    }

    void IDisposable.Dispose()
    {
        (Value as IDisposable)?.Dispose();
    }
}

public static class BinaryExtensions
{
    public static Base64<TValue> ToBase64<TValue>(this TValue value) where TValue : IReadOnlyList<byte>
    {
        return value;
    }

    public static Hex<TValue> ToHex<TValue>(this TValue value) where TValue : IReadOnlyList<byte>
    {
        return value;
    }
}
