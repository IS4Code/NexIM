using System;

namespace NexIM.Primitives;

public readonly struct ValueUri : IEquatable<ValueUri>
{
    static readonly Uri defaultUri = new Uri("", UriKind.Relative);

    readonly Uri? _uri;
    Uri uri => _uri ?? defaultUri;

    private ValueUri(Uri value)
    {
        _uri = value;
    }

    public ValueUri(string value) : this(new Uri(value, UriKind.RelativeOrAbsolute))
    {
        // TODO Should not parse
    }

    public static ValueUri Parse(string str)
    {
        return new(new Uri(str, UriKind.RelativeOrAbsolute));
    }

    public bool Equals(ValueUri other)
    {
        return uri.OriginalString == other.uri.OriginalString;
    }

    public override bool Equals(object obj)
    {
        return obj is ValueUri uri && Equals(uri);
    }

    public override int GetHashCode()
    {
        return uri.GetHashCode();
    }

    public override string ToString()
    {
        return uri.OriginalString;
    }
}
