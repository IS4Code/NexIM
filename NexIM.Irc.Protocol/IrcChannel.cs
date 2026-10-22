using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace NexIM.Irc.Protocol;

/// <summary>
/// Identifies an IRC channel, with an optional host mask.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct IrcChannel : IEquatable<IrcChannel>, IComparable<IrcChannel>
{
    public ByteStringView Value { get; }
    readonly int type, maskStart;

    public IrcChannelType Type => (IrcChannelType)type;

    public ByteStringView Name {
        get {
            if(maskStart >= Value.Length)
            {
                // Bare name
                return Value.Slice(1);
            }
            return Value.Slice(1, maskStart - 1);
        }
    }

    public ByteStringView Mask => Value.Slice(maskStart);

    internal (int, int) ParseData => (type, maskStart);

    internal IrcChannel(ByteStringView value, int type, int maskStart)
    {
        Value = value;
        this.type = type;
        this.maskStart = maskStart;
    }

    public IrcChannel(ByteStringView value)
    {
        if(!TryParse(value, out this, out var exception))
        {
            throw exception;
        }
    }

    public IrcChannel(IrcChannelType type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> mask = default)
    {
        if(!TryCreate(type, name, mask, out this, out var exception))
        {
            throw exception;
        }
    }

    public static bool TryCreate(IrcChannelType type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> mask, out IrcChannel address)
    {
        return TryCreate(type, name, mask, out address, out _);
    }

    static ReadOnlySpan<byte> partInvalid => "\0\x07\r\n ,:"u8;
    private static bool TryCreate(IrcChannelType type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> mask, out IrcChannel address, [NotNullWhen(false)] out Exception? exception)
    {
        if(type is not IrcChannelType.Normal or IrcChannelType.Local or IrcChannelType.Modeless or IrcChannelType.Safe)
        {
            address = default;
            exception = new InvalidEnumArgumentException("Type is invalid.", (int)type, typeof(IrcChannelType));
            return false;
        }
        if(name.IsEmpty)
        {
            address = default;
            exception = new ArgumentException("Name is empty.", nameof(name));
            return false;
        }
        if(name.IndexOfAny(partInvalid) != -1)
        {
            address = default;
            exception = new ArgumentException("Name contains unsupported characters.", nameof(name));
            return false;
        }
        if(mask.IndexOfAny(partInvalid) != -1)
        {
            address = default;
            exception = new ArgumentException("Mask contains unsupported characters.", nameof(mask));
            return false;
        }
        byte[] buffer;
        int maskStart;
        if(mask.IsEmpty)
        {
            buffer = new byte[1 + name.Length];
            buffer[0] = (byte)type;
            name.CopyTo(buffer.AsSpan(1));
            maskStart = buffer.Length;
        }
        else
        {
            int maskAt = 2 + name.Length;
            buffer = new byte[maskAt + mask.Length];
            buffer[0] = (byte)type;
            name.CopyTo(buffer.AsSpan(1));
            buffer[1 + name.Length] = (byte)':';
            mask.CopyTo(buffer.AsSpan(maskAt));
            maskStart = maskAt;
        }
        address = new(buffer, (int)type, maskStart);
        exception = null;
        return true;
    }

    public static bool TryParse(ByteStringView value, out IrcChannel address)
    {
        return TryParse(value, out address, out _);
    }

    private static bool TryParse(ByteStringView value, out IrcChannel address, [NotNullWhen(false)] out Exception? exception)
    {
        var span = value.AsSpan();
        if(span.Length <= 1)
        {
            address = default;
            exception = new ArgumentException("Name is empty.", nameof(value));
            return false;
        }
        var type = (IrcChannelType)span[0];
        if(type is not IrcChannelType.Normal or IrcChannelType.Local or IrcChannelType.Modeless or IrcChannelType.Safe)
        {
            address = default;
            exception = new ArgumentException("Type is invalid.", nameof(value));
            return false;
        }
        int maskAt = span.Slice(1).IndexOf((byte)':');
        int maskStart;
        if(maskAt == -1)
        {
            // No mask portion

            maskStart = value.Length;
        }
        else
        {
            maskStart = maskAt + 2;
            if(span.Slice(maskStart).IsEmpty)
            {
                address = default;
                exception = new ArgumentException("Mask is empty.", nameof(value));
                return false;
            }
        }
        address = new(value, (int)type, maskStart);
        exception = null;
        return true;
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public string ToString(Encoding encoding)
    {
        return Value.ToString(encoding);
    }

    public bool Equals(IrcChannel other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is IrcEndPoint other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(IrcChannel other)
    {
        return Value.CompareTo(other.Value);
    }

    public static bool operator ==(IrcChannel a, IrcChannel b) => a.Equals(b);
    public static bool operator !=(IrcChannel a, IrcChannel b) => !a.Equals(b);
}

public enum IrcChannelType : byte
{
    Normal = (byte)'#',
    Local = (byte)'&',
    Safe = (byte)'!',
    Modeless = (byte)'+'
}
