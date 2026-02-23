using System;
using System.Runtime.InteropServices;

namespace Unicord.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppResource(XmppAddress Address, string? ResourceIdentifier)
{
    bool Validated { get; init; }

    public const int MaxLength = XmppAddress.MaxComponentLength * 3 + 2;

    public XmppResource Bare => new(Address, null);

    public XmppResource(string? user, string host, string? resourceIdentifier) : this(new XmppAddress(user, host), resourceIdentifier)
    {

    }

    public bool IsWiderThan(XmppResource? other)
    {
        if(other is not { } value)
        {
            return false;
        }
        return
            Address == value.Address &&
            (ResourceIdentifier == null || ResourceIdentifier == value.ResourceIdentifier);
    }

    public bool IsNarrowerThan(XmppResource? other)
    {
        if(other is not { } value)
        {
            return true;
        }
        return value.IsWiderThan(this);
    }

    public static XmppResource Parse(ReadOnlySpan<char> span)
    {
        int resourceStart = span.IndexOf('/');
        if(resourceStart == -1)
        {
            return new(XmppAddress.Parse(span), null);
        }
        else
        {
            var resourcePart = span.Slice(resourceStart + 1);
            XmppAddress.ValidateResourceComponent(resourcePart);
            return new(XmppAddress.Parse(span.Slice(0, resourceStart)), resourcePart.ToString())
            {
                Validated = true
            };
        }
    }

    public static XmppResource Parse(string text)
    {
        int resourceStart = text.IndexOf('/');
        if(resourceStart == -1)
        {
            return new(XmppAddress.Parse(text), null);
        }
        else
        {
            var resourcePart = text.AsSpan(resourceStart + 1);
            XmppAddress.ValidateResourceComponent(resourcePart);
            return new(XmppAddress.Parse(text.AsSpan(0, resourceStart)), resourcePart.ToString())
            {
                Validated = true
            };
        }
    }

    public override string ToString()
    {
        bool validate = !Validated;
        if(ResourceIdentifier == null)
        {
            return Address.ToString(validate);
        }
        if(validate)
        {
            XmppAddress.ValidateResourceComponent(ResourceIdentifier.AsSpan());
        }
        return $"{Address.ToString(validate)}/{ResourceIdentifier}";
    }
}
