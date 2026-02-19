using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Unicord.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppAddress(string? User, string Host)
{
    static readonly Regex addressRegex = new("^(?:(.{1,1023})@)?([^@/]{1,1023})$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

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

    public static XmppAddress Parse(string? text)
    {
        if(text == null || addressRegex.Match(text) is not { Success: true } match)
        {
            throw new ArgumentException("The address is invalid.", nameof(text), XmppStanzaException.JidMalformed());
        }
        var user = match.Groups[1];
        return new(
            user.Success ? user.Value : null,
            match.Groups[2].Value
        );
    }

    public override string ToString()
    {
        if(User == null)
        {
            return Host;
        }
        return $"{User}@{Host}";
    }
}
