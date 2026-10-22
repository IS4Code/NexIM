using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace NexIM.Irc.Protocol;

/// <summary>
/// Represents the destination of an IRC text message, as a concrete end-point, user, or channel.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct IrcTarget : IEquatable<IrcTarget>, IComparable<IrcTarget>
{
    public IrcIdentifierKind Kind { get; }
    public ByteStringView Value { get; }

    readonly int parse1, parse2;

    private IrcTarget(IrcIdentifierKind kind, ByteStringView value, (int, int) parseData)
    {
        Kind = kind;
        Value = value;
        (parse1, parse2) = parseData;
    }

    public IrcTarget(IrcEndPoint address) : this(IrcIdentifierKind.EndPoint, address.Value, address.ParseData)
    {

    }

    public IrcTarget(IrcUser address) : this(IrcIdentifierKind.User, address.Value, address.ParseData)
    {

    }

    public IrcTarget(IrcChannel address) : this(IrcIdentifierKind.Channel, address.Value, address.ParseData)
    {

    }

    public IrcTarget(ByteStringView value)
    {
        if(!TryParse(value, out this, out var exception))
        {
            throw exception;
        }
    }

    public bool TryToEndPoint(out IrcEndPoint address)
    {
        if(Kind == IrcIdentifierKind.EndPoint)
        {
            address = new(Value, parse1, parse2);
            return true;
        }
        address = default;
        return false;
    }

    public bool TryToUser(out IrcUser address)
    {
        if(Kind == IrcIdentifierKind.User)
        {
            address = new(Value, parse1, parse2);
            return true;
        }
        address = default;
        return false;
    }

    public bool TryToChannel(out IrcChannel address)
    {
        if(Kind == IrcIdentifierKind.Channel)
        {
            address = new(Value, parse1, parse2);
            return true;
        }
        address = default;
        return false;
    }

    public static bool TryParse(ByteStringView value, out IrcTarget address)
    {
        return TryParse(value, out address, out _);
    }

    private static bool TryParse(ByteStringView value, out IrcTarget address, [NotNullWhen(false)] out Exception? exception)
    {
        if(value.IsEmpty)
        {
            address = default;
            exception = new ArgumentException("The address is empty.", nameof(value));
            return false;
        }
        var span = value.AsSpan();
        if(span[0] == (byte)'$')
        {
            // Or #mask but that all such masks are valid channels
            address = default;
            exception = new ArgumentException("Host masks are not supported.", nameof(value));
            return false;
        }
        if(IrcChannel.TryParse(value, out var channel))
        {
            // Priority - decides based on the first character
            address = channel;
            exception = null;
            return true;
        }
        if(span.IndexOf((byte)'!') == -1 && IrcUser.TryParse(value, out var user))
        {
            // Hopefully no username with !
            address = user;
            exception = null;
            return true;
        }
        if(IrcEndPoint.TryParse(value, out var endPoint))
        {
            address = endPoint;
            exception = null;
            return true;
        }
        address = default;
        exception = new ArgumentException("The address has unrecognized format.", nameof(value));
        return false;
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public string ToString(Encoding encoding)
    {
        return Value.ToString(encoding);
    }

    public bool Equals(IrcTarget other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is IrcTarget other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(IrcTarget other)
    {
        return Value.CompareTo(other.Value);
    }

    public static bool operator ==(IrcTarget a, IrcTarget b) => a.Equals(b);
    public static bool operator !=(IrcTarget a, IrcTarget b) => !a.Equals(b);

    public static implicit operator IrcTarget(IrcEndPoint address) => new(address);
    public static implicit operator IrcTarget(IrcUser address) => new(address);
    public static implicit operator IrcTarget(IrcChannel address) => new(address);
}

public enum IrcIdentifierKind
{
    EndPoint,
    User,
    Channel
}
