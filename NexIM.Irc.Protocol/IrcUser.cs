using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace NexIM.Irc.Protocol;

/// <summary>
/// Identifies an active user in an IRC network, by a local or remote host.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct IrcUser : IEquatable<IrcUser>, IComparable<IrcUser>
{
    public ByteStringView Value { get; }
    readonly int hostStart, serverStart;

    internal (int, int) ParseData => (hostStart, serverStart);

    public ByteStringView User {
        get {
            int nextStart = Math.Min(hostStart, serverStart);
            if(nextStart >= Value.Length)
            {
                // Bare user
                return Value;
            }
            else
            {
                return Value.Slice(0, nextStart - 1);
            }
        }
    }

    public ByteStringView Host {
        get {
            if(hostStart == serverStart)
            {
                // No host
                return default;
            }
            else if(serverStart >= Value.Length)
            {
                // No server
                return Value.Slice(hostStart);
            }
            else
            {
                return Value.Slice(hostStart, serverStart - hostStart - 1);
            }
        }
    }

    public ByteStringView Server => Value.Slice(serverStart);

    internal IrcUser(ByteStringView value, int userStart, int hostStart)
    {
        Value = value;
        this.hostStart = userStart;
        this.serverStart = hostStart;
    }

    public IrcUser(ByteStringView value)
    {
        if(!TryParse(value, out this, out var exception))
        {
            throw exception;
        }
    }

    public IrcUser(ReadOnlySpan<byte> user, ReadOnlySpan<byte> host = default, ReadOnlySpan<byte> server = default)
    {
        if(!TryCreate(user, host, server, out this, out var exception))
        {
            throw exception;
        }
    }

    public static bool TryCreate(ReadOnlySpan<byte> user, out IrcUser address)
    {
        return TryCreate(user, default, default, out address, out _);
    }

    public static bool TryCreate(ReadOnlySpan<byte> user, ReadOnlySpan<byte> host, out IrcUser address)
    {
        return TryCreate(user, host, default, out address, out _);
    }

    public static bool TryCreate(ReadOnlySpan<byte> user, ReadOnlySpan<byte> host, ReadOnlySpan<byte> server, out IrcUser address)
    {
        return TryCreate(user, host, server, out address, out _);
    }

    private static bool TryCreate(ReadOnlySpan<byte> user, ReadOnlySpan<byte> host, ReadOnlySpan<byte> server, out IrcUser address, [NotNullWhen(false)] out Exception? exception)
    {
        if(host.IsEmpty && server.IsEmpty)
        {
            address = default;
            exception = new ArgumentException("Host or server must be specified.", nameof(host));
            return false;
        }
        if(user.IndexOfAny("%@"u8) != -1)
        {
            address = default;
            exception = new ArgumentException("User contains unsupported characters.", nameof(user));
            return false;
        }
        if(host.IndexOf((byte)'@') != -1)
        {
            address = default;
            exception = new ArgumentException("Host contains unsupported characters.", nameof(host));
            return false;
        }
        byte[] buffer;
        int hostStart, serverStart;
        if(host.IsEmpty)
        {
            // user@server
            int serverAt = user.Length + 1;
            buffer = new byte[serverAt + server.Length];
            user.CopyTo(buffer);
            buffer[user.Length] = (byte)'@';
            server.CopyTo(buffer.AsSpan(serverAt));
            hostStart = serverStart = serverAt;
        }
        else
        {
            if(server.IsEmpty)
            {
                // user%host
                int hostAt = user.Length + 1;
                buffer = new byte[hostAt + host.Length];
                user.CopyTo(buffer);
                buffer[user.Length] = (byte)'%';
                host.CopyTo(buffer.AsSpan(hostAt));
                hostStart = hostAt;
                serverStart = buffer.Length;
            }
            else
            {
                // user%host@server
                int hostAt = user.Length + 1;
                int serverAt = hostAt + host.Length + 1;
                buffer = new byte[serverAt + server.Length];
                user.CopyTo(buffer);
                buffer[user.Length] = (byte)'%';
                host.CopyTo(buffer.AsSpan(hostAt));
                buffer[hostAt + host.Length] = (byte)'@';
                server.CopyTo(buffer.AsSpan(serverAt));
                hostStart = hostAt;
                serverStart = serverAt;
            }
        }
        address = new(buffer, hostStart, serverStart);
        exception = null;
        return true;
    }

    public static bool TryParse(ByteStringView value, out IrcUser address)
    {
        return TryParse(value, out address, out _);
    }

    private static bool TryParse(ByteStringView value, out IrcUser address, [NotNullWhen(false)] out Exception? exception)
    {
        var span = value.AsSpan();
        int hostAt = span.IndexOf((byte)'%');
        int hostStart, serverStart;
        if(hostAt == -1)
        {
            // No host portion

            int serverAt = span.IndexOf((byte)'@');
            if(serverAt == -1)
            {
                address = default;
                exception = new ArgumentException("Both host and server portion is not empty.", nameof(value));
                return false;
            }

            hostStart = serverStart = serverAt + 1;
        }
        else
        {
            hostStart = hostAt + 1;

            int serverAt = span.Slice(hostStart).IndexOf((byte)'@');
            if(serverAt == -1)
            {
                // No server portion
                serverStart = span.Length;
            }
            else
            {
                serverStart = hostStart + serverAt + 1;
            }
        }
        address = new(value, hostStart, serverStart);
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

    public bool Equals(IrcUser other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is IrcUser other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(IrcUser other)
    {
        return Value.CompareTo(other.Value);
    }

    public static bool operator ==(IrcUser a, IrcUser b) => a.Equals(b);
    public static bool operator !=(IrcUser a, IrcUser b) => !a.Equals(b);
}
