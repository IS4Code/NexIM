using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Unicord.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppAddress(string? User, string Host)
{
    public const int MaxComponentLength = 1023;

    static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;
    static readonly Encoding encoding = new UTF8Encoding(false);

    public bool Equals(XmppAddress other)
    {
        return
            comparer.Equals(User, other.User) &&
            comparer.Equals(Host, other.Host);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(User, comparer);
        hash.Add(Host, comparer);
        return hash.ToHashCode();
    }

    internal static XmppAddress Parse(ReadOnlySpan<char> span)
    {
        int hostAt = span.IndexOf('@');
        if(hostAt == -1)
        {
            ValidateHostComponent(span);
            return new(null, span.ToString());
        }

        var user = span.Slice(0, hostAt);
        ValidateLocalComponent(user);

        var host = span.Slice(hostAt + 1);
        ValidateHostComponent(host);

        return new(user.ToString(), host.ToString());
    }

    internal static XmppAddress Parse(string text)
    {
        int hostAt = text.IndexOf('@');
        if(hostAt == -1)
        {
            ValidateHostComponent(text.AsSpan());
            return new(null, text);
        }

        var user = text.AsSpan(0, hostAt);
        ValidateLocalComponent(user);

        var host = text.AsSpan(hostAt + 1);
        ValidateHostComponent(host);

        return new(user.ToString(), host.ToString());
    }

    static void ValidateComponentLength(ReadOnlySpan<char> span)
    {
        int length = span.Length;
        if(length == 0)
        {
            throw new ArgumentException("The address has a zero-length component.", nameof(span), XmppStanzaException.JidMalformed());
        }
        if(
            // Never less bytes than characters (incl. surrogates)
            length > MaxComponentLength ||
            (
                // Check if there is a potential to go over the limit (from 341)
                encoding.GetMaxByteCount(length) > MaxComponentLength &&
                GetByteCount(span) > MaxComponentLength
            ))
        {
            throw new ArgumentException($"The address has a component over the length limit of {MaxComponentLength}.", nameof(span), XmppStanzaException.JidMalformed());
        }
    }

    // TODO Ensure normalization and character classes

    static readonly char[] localDisallowedCharacters = { '"', '&', '\'', '/', ':', '<', '>', '@' };

    internal static void ValidateLocalComponent(ReadOnlySpan<char> span)
    {
        ValidateComponentLength(span);

        if(span.IndexOfAny(localDisallowedCharacters) != -1)
        {
            throw new ArgumentException($"The local address component contains disallowed characters.", nameof(span), XmppStanzaException.JidMalformed());
        }
    }

    internal static void ValidateHostComponent(ReadOnlySpan<char> span)
    {
        ValidateComponentLength(span);
    }

    internal static void ValidateResourceComponent(ReadOnlySpan<char> span)
    {
        ValidateComponentLength(span);
    }

    static unsafe int GetByteCount(ReadOnlySpan<char> span)
    {
        fixed(char* buffer = span)
        {
            return encoding.GetByteCount(buffer, span.Length);
        }
    }

    internal string ToString(bool validate)
    {
        var host = Host;
        if(validate)
        {
            ValidateHostComponent(host.AsSpan());
        }

        var user = User;
        if(user == null)
        {
            return host;
        }

        if(validate)
        {
            ValidateLocalComponent(user.AsSpan());
        }

        return $"{user}@{host}";
    }

    public override string ToString()
    {
        return ToString(false);
    }
}
