using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using NexIM.Primitives.Xml;

namespace NexIM.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppAddress : IEquatable<XmppAddress>
{
    public const int MaxComponentLength = 1023;

    static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;
    static readonly Encoding encoding = new UTF8Encoding(false);

    public string? User { get; }
    public string Host { get; }

    public XmppAddress HostOnly => new(null, Host, false);

    internal XmppAddress(ReadOnlyMemory<char>? user, ReadOnlyMemory<char> host, XmlNameTable? nameTable, bool validate)
    {
        if(user is { } u)
        {
            if(validate)
            {
                ValidateLocalComponent(u.Span);
            }
            User = GetString(u);
        }

        if(validate)
        {
            ValidateHostComponent(host.Span);
        }
        Host = GetString(host);

        string GetString(ReadOnlyMemory<char> memory)
        {
            if(nameTable == null)
            {
                return memory.ToString();
            }
            return nameTable.Add(memory);
        }
    }

    internal XmppAddress(string? user, string host, bool validate) : this(user?.AsMemory(), host.AsMemory(), null, validate)
    {

    }

    public XmppAddress(ReadOnlyMemory<char>? user, ReadOnlyMemory<char> host, XmlNameTable? nameTable) : this(user, host, nameTable, true)
    {

    }

    public XmppAddress(string? user, string host) : this(user, host, true)
    {

    }

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

    public static XmppAddress Parse(ReadOnlyMemory<char> data, XmlNameTable? nameTable)
    {
        var span = data.Span;

        int hostAt = span.IndexOf('@');
        if(hostAt == -1)
        {
            return new(null, data, nameTable);
        }

        return new(data.Slice(0, hostAt), data.Slice(hostAt + 1), nameTable);
    }

    public static XmppAddress Parse(string data)
    {
        return Parse(data.AsMemory(), null);
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

    public override string ToString()
    {
        var host = Host;
        if(host == null)
        {
            throw new InvalidOperationException("The instance has been default-initialized.");
        }

        var user = User;
        if(user == null)
        {
            return host;
        }

        return $"{user}@{host}";
    }
}
