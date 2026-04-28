using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NexIM.Tools;

namespace NexIM.Primitives;

[StructLayout(LayoutKind.Auto)]
public readonly partial struct LocalizedString : IEquatable<LocalizedString>, IEnumerable<LanguageTaggedString>
{
    readonly NonEmptyDictionary<LanguageCode, ValueString> data;

    private LocalizedString(NonEmptyDictionary<LanguageCode, ValueString> data)
    {
        this.data = data;
    }

    public LocalizedString(LanguageTaggedString initial)
    {
        this.data = new(new(initial.Language, new(initial.Value)));
    }

    public LocalizedString Add(LanguageTaggedString other)
    {
        return new(data.Add(new(other.Language, new(other.Value))));
    }

    public LocalizedString Add(LanguageTaggedString? other)
    {
        return other is { } value ? Add(value) : this;
    }

    public LocalizedString Add(LocalizedString other)
    {
        return new(data.AddRange(other.data));
    }

    public LocalizedString Add(LocalizedString? other)
    {
        return other is { } value ? Add(value) : this;
    }

    public override bool Equals(object obj)
    {
        return obj is LocalizedString other && Equals(other);
    }

    public bool Equals(LocalizedString other)
    {
        return data.Equals(other.data);
    }

    public static bool operator ==(LanguageCode a, LocalizedString b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(LanguageCode a, LocalizedString b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return data.GetHashCode();
    }

    public Enumerator GetEnumerator()
    {
        return new(data.GetEnumerator());
    }

    IEnumerator<LanguageTaggedString> IEnumerable<LanguageTaggedString>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return data.ToString();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<LanguageTaggedString>
    {
        NonEmptyDictionary<LanguageCode, ValueString>.Enumerator enumerator;

        internal Enumerator(NonEmptyDictionary<LanguageCode, ValueString>.Enumerator enumerator)
        {
            this.enumerator = enumerator;
        }

        public readonly LanguageTaggedString Current {
            get {
                var current = enumerator.Current;
                return new(current.Value.Value, current.Key);
            }
        }

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return enumerator.MoveNext();
        }

        public void Reset()
        {
            enumerator.Reset();
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }
    }
}
