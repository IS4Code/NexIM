using System;
using System.Runtime.InteropServices;
using System.Xml;
using NexIM.Primitives.Xml;

namespace NexIM.Xmpp.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct XmppResource
{
    public const int MaxLength = XmppAddress.MaxComponentLength * 3 + 2;

    public XmppAddress Address { get; }
    public string? ResourceIdentifier { get; }

    public XmppResource Bare => new(Address, null, false);

    internal XmppResource(XmppAddress address, ReadOnlyMemory<char>? resourceIdentifier, XmlNameTable? nameTable, bool validate)
    {
        if(resourceIdentifier is { } data)
        {
            if(validate)
            {
                XmppAddress.ValidateResourceComponent(data.Span);
            }
            ResourceIdentifier = GetString(data);
        }

        Address = address;

        string GetString(ReadOnlyMemory<char> memory)
        {
            if(nameTable == null)
            {
                return memory.ToString();
            }
            return nameTable.Add(memory);
        }
    }

    internal XmppResource(XmppAddress address, string? resourceIdentifier, bool validate) : this(address, resourceIdentifier?.AsMemory(), null, validate)
    {

    }

    public XmppResource(ReadOnlyMemory<char>? user, ReadOnlyMemory<char> host, ReadOnlyMemory<char>? resourceIdentifier, XmlNameTable? nameTable) : this(new XmppAddress(user, host, nameTable), resourceIdentifier, nameTable, true)
    {

    }

    public XmppResource(string? user, string host, string? resourceIdentifier) : this(new XmppAddress(user, host), resourceIdentifier, true)
    {

    }

    public XmppResource(XmppAddress address, ReadOnlyMemory<char>? resourceIdentifier, XmlNameTable? nameTable) : this(address, resourceIdentifier, nameTable, true)
    {

    }

    public XmppResource(XmppAddress address, string? resourceIdentifier) : this(address, resourceIdentifier, true)
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

    public static XmppResource Parse(ReadOnlyMemory<char> data, XmlNameTable? nameTable)
    {
        var span = data.Span;
        int resourceStart = span.IndexOf('/');
        if(resourceStart == -1)
        {
            return new(XmppAddress.Parse(data, nameTable), null);
        }
        else
        {
            var addressPart = data.Slice(0, resourceStart);
            var resourcePart = data.Slice(resourceStart + 1);
            return new(XmppAddress.Parse(addressPart, nameTable), resourcePart, nameTable);
        }
    }

    public static XmppResource Parse(string data)
    {
        return Parse(data.AsMemory(), null);
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
