using System;
using System.Runtime.InteropServices;

namespace Unicord.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppAddress(string? User, string Host)
{
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

    public override string ToString()
    {
        if(User == null)
        {
            return Host;
        }
        return $"{User}@{Host}";
    }
}
