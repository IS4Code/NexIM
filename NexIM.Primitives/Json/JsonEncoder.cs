using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NexIM.Primitives.Json.Grammar;

namespace NexIM.Primitives.Json;

using XmlConvert = System.Xml.XmlConvert;

/// <summary>
/// Provides support for encoding to JSON.
/// </summary>
public abstract class JsonEncoder :
    IValueJsonEncoder<Token<Enum>>,
    IValueJsonEncoder<DateTime>,
    IValueJsonEncoder<DateTimeOffset>,
    IValueJsonEncoder<TimeSpan>,
    IValueJsonEncoder<DateComponents>,
    IValueJsonEncoder<TimeZoneOffset>,
    IValueJsonEncoder<ValueUri>,
    IValueJsonEncoder<(LanguageCode language, string text)>,
    IValueJsonEncoder<LanguageTaggedString>,
    IValueJsonEncoder<LocalizedString>
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

    ValueTask IValueJsonEncoder<TimeSpan>.Encode(JsonWriter writer, TimeSpan value)
    {
        return new(writer.WriteValueAsync(XmlConvert.ToString(value)));
    }

    ValueTask IValueJsonEncoder<DateComponents>.Encode(JsonWriter writer, DateComponents value)
    {
        return new(writer.WriteValueAsync(value.ToString()));
    }

    ValueTask IValueJsonEncoder<TimeZoneOffset>.Encode(JsonWriter writer, TimeZoneOffset value)
    {
        return new(writer.WriteValueAsync(value.ToString()));
    }

    ValueTask IValueJsonEncoder<ValueUri>.Encode(JsonWriter writer, ValueUri value)
    {
        return new(writer.WriteValueAsync(value.ToString()));
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

    /// <summary>
    /// The paths of the currently open properties via <see cref="EnterProperty(JsonWriter, string, ValueKind)"/>.
    /// </summary>
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

        if(writer.WriteState == WriteState.Array && expectedState == WriteState.Array && containingPath != null && PathEndsWith(containingPath, name))
        {
            // Inside an array property already
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
    }

    private string? GetContainingPath(string path)
    {
        // There may be top scopes remaining from already exited properties
        while(scopes.Count > 0)
        {
            var top = scopes.Peek();
            if(PathStartsWith(path, top))
            {
                // This scope is still up-to-date
                return top;
            }
            // Remove non-containing path
            scopes.Pop();
        }
        return null;
    }

    private Task ExitProperty(JsonWriter writer, string path, string? containingPath)
    {
        if(path != containingPath && writer.WriteState != WriteState.Array)
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
            // Starts the string or is preceded by member access
            (path.Length == member.Length || path[path.Length - member.Length - 1] == '.');
    }

    private static bool PathStartsWith(string path, string prefix)
    {
        return
            path.StartsWith(prefix, StringComparison.Ordinal) &&
            // Ends the string or is followed by separator
            (path.Length == prefix.Length || path[prefix.Length] is '.' or '[');
    }

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

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Used from generated code")]
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
