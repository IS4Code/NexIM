using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Unicord.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppResource(XmppAddress Address, string? ResourceIdentifier)
{
    static readonly Regex resourceRegex = new("^(?:(.{1,1023})@)?([^@/]{1,1023})(?:/(.{1,1023}))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public XmppResource(string? user, string host, string? resourceIdentifier) : this(new(user, host), resourceIdentifier)
    {

    }

    public static XmppResource Parse(string text)
    {
        if(resourceRegex.Match(text) is not { Success: true } match)
        {
            throw new ArgumentException("The resource address is invalid.", nameof(text));
        }
        var user = match.Groups[1];
        var resource = match.Groups[3];
        return new(
            user.Success ? user.Value : null,
            match.Groups[2].Value,
            resource.Success ? resource.Value : null
        );
    }

    public override string ToString()
    {
        if(ResourceIdentifier == null)
        {
            return Address.ToString();
        }
        return $"{Address}/{ResourceIdentifier}";
    }
}
