using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Unicord.Primitives.Json.Grammar;

namespace Unicord.Primitives.Json;

using XmlConvert = System.Xml.XmlConvert;

/// <summary>
/// Provides support for encoding to JSON.
/// </summary>
public abstract class JsonEncoder : IValueJsonEncoder<Token<Enum>>, IValueJsonEncoder<DateTime>, IValueJsonEncoder<DateTimeOffset>, IValueJsonEncoder<Uri>, IValueJsonEncoder<(LanguageCode language, string text)>, IValueJsonEncoder<LanguageTaggedString>, IValueJsonEncoder<LocalizedString>
{
    protected abstract JsonWriter Writer { get; }

    protected ValueTask Encode<T, TEncoder>(JsonWriter writer, T value, TEncoder encoder) where TEncoder : IValueJsonEncoder<T>
    {
        return encoder.Encode(writer, value);
    }

    protected async ValueTask EncodeTokenAsync(JsonWriter writer, string tokenValue)
    {
        await writer.WriteValueAsync(tokenValue);
    }

    ValueTask IValueJsonEncoder<Token<Enum>>.Encode(JsonWriter writer, Token<Enum> token)
    {
        return EncodeTokenAsync(writer, token.Value);
    }

    async ValueTask IValueJsonEncoder<DateTime>.Encode(JsonWriter writer, DateTime value)
    {
        if(value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date must be in UTC.", nameof(value));
        }
        await writer.WriteValueAsync(XmlConvert.ToString(value, System.Xml.XmlDateTimeSerializationMode.Utc));
    }

    ValueTask IValueJsonEncoder<DateTimeOffset>.Encode(JsonWriter writer, DateTimeOffset value)
    {
        return new(writer.WriteValueAsync(XmlConvert.ToString(value)));
    }

    async ValueTask IValueJsonEncoder<Uri>.Encode(JsonWriter writer, Uri value)
    {
        await writer.WriteValueAsync(value.OriginalString);
    }

    async ValueTask IValueJsonEncoder<(LanguageCode language, string text)>.Encode(JsonWriter writer, (LanguageCode, string) value)
    {
        // Atomic language property
        var (language, text) = value;
        await writer.WritePropertyNameAsync(language.IsEmpty ? "default" : language.Value);
        await writer.WriteValueAsync(text);
    }

    async ValueTask IValueJsonEncoder<LanguageTaggedString>.Encode(JsonWriter writer, LanguageTaggedString value)
    {
        // Only used in an attribute
        await writer.WriteStartObjectAsync();
        await Encode(writer, (value.Language, value.Value), this);
        await writer.WriteEndObjectAsync();
    }

    async ValueTask IValueJsonEncoder<LocalizedString>.Encode(JsonWriter writer, LocalizedString value)
    {
        await writer.WriteStartObjectAsync();
        foreach(var str in value)
        {
            await Encode(writer, (str.Language, str.Value), this);
        }
        await writer.WriteEndObjectAsync();
    }

    readonly Stack<string> scopes = new();

    protected async ValueTask EnterProperty(JsonWriter writer, string name, ValueKind kind)
    {
        string path = writer.Path;
        string? containingPath = GetContainingPath(path);

        var expectedState = kind switch {
            ValueKind.Array => WriteState.Array,
            ValueKind.Object => WriteState.Object,
            _ => WriteState.Property
        };

        if(kind == ValueKind.Scalar)
        {
            // Close any scope
            await ExitProperty(writer, path, containingPath);
            return;
        }

        if(writer.WriteState == expectedState && containingPath == path && PathEndsWith(path, name))
        {
            // Already in the scope
            return;
        }

        await ExitProperty(writer, path, containingPath);
        await writer.WritePropertyNameAsync(name);
        switch(kind)
        {
            case ValueKind.Array:
                await writer.WriteStartArrayAsync();
                break;
            case ValueKind.Object:
                await writer.WriteStartObjectAsync();
                break;
        }
        scopes.Push(!String.IsNullOrEmpty(path) ? path + "." + name : name);

        //return writerData.GetOrCreateValue(writer).EnterProperty(writer, name, kind);
    }

    private string? GetContainingPath(string path)
    {
        while(scopes.Count > 0)
        {
            // Remove non-containing paths
            var top = scopes.Peek();
            if(PathStartsWith(path, top))
            {
                return top;
            }
            scopes.Pop();
        }
        return null;
    }

    private Task ExitProperty(JsonWriter writer, string path, string? containingPath)
    {
        if(path != containingPath)
        {
            // Not in a scope path
            return Task.CompletedTask;
        }
        scopes.Pop();
        switch(writer.WriteState)
        {
            case WriteState.Array:
                return writer.WriteEndArrayAsync();
            case WriteState.Object:
                return writer.WriteEndObjectAsync();
            default:
                return Task.CompletedTask;
        }
    }

    private static bool PathEndsWith(string path, string member)
    {
        return
            path.EndsWith(member, StringComparison.Ordinal) &&
            (path.Length == member.Length || path[path.Length - member.Length - 1] == '.');
    }

    private static bool PathStartsWith(string path, string prefix)
    {
        return
            path.StartsWith(prefix, StringComparison.Ordinal) &&
            (path.Length == prefix.Length || path[prefix.Length] == '.');
    }

    static readonly ConditionalWeakTable<JsonWriter, JsonWriterExtra> writerData = new();

    sealed class JsonWriterExtra
    {
        public async ValueTask EnterProperty(JsonWriter writer, string name, ValueKind kind)
        {
            switch(kind)
            {
                case ValueKind.Array:
                    if(writer.WriteState == WriteState.Array)
                    {

                    }
                    else
                    {
                        await writer.WritePropertyNameAsync(name);
                        await writer.WriteStartArrayAsync();
                    }
                    break;
                case ValueKind.Object:
                default:
                    if(writer.WriteState == WriteState.Array)
                    {
                        await writer.WriteEndArrayAsync();
                    }
                    await writer.WritePropertyNameAsync(name);
                    break;
            }
        }

        public ValueTask WriteLanguageString(JsonWriter writer, LanguageTaggedString value)
        {
            return default;
        }
    }
}

public interface IValueJsonEncoder<in T>
{
    ValueTask Encode(JsonWriter writer, T value);
}
