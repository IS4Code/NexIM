using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace NexIM.Irc.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly struct IrcAddress : IEquatable<IrcAddress>, IComparable<IrcAddress>
{
    public ByteStringView Value { get; }
    readonly int userStart, hostStart;

    public bool IsEmpty => Value.IsEmpty;

    public ByteStringView Nickname {
        get {
            int nextStart = Math.Min(userStart, hostStart);
            if(nextStart == 0)
            {
                // Bare host
                return default;
            }
            else if(nextStart >= Value.Length)
            {
                // Bare nickname
                return Value;
            }
            else
            {
                return Value.Slice(0, nextStart - 1);
            }
        }
    }

    public ByteStringView User {
        get {
            if(userStart == hostStart)
            {
                // No user
                return default;
            }
            else if(hostStart >= Value.Length)
            {
                // No host
                return Value.Slice(userStart);
            }
            else
            {
                return Value.Slice(userStart, hostStart - userStart - 1);
            }
        }
    }

    public ByteStringView Host => Value.Slice(hostStart);

    static readonly ByteStringView starTarget = "*"u8.ToArray();
    public ByteStringView ReplyTarget {
        get {
            var nickname = Nickname;
            return nickname.IsEmpty ? starTarget : nickname;
        }
    }

    private IrcAddress(ByteStringView value, int userStart, int hostStart)
    {
        Value = value;
        this.userStart = userStart;
        this.hostStart = hostStart;
    }

    public IrcAddress(ByteStringView value, Components allowedComponents = (Components)(-1))
    {
        if(!TryParse(value, allowedComponents, out this, out var exception))
        {
            throw exception;
        }
    }

    public IrcAddress(ReadOnlySpan<byte> nickname = default, ReadOnlySpan<byte> user = default, ReadOnlySpan<byte> host = default)
    {
        if(!TryCreate(nickname, user, host, out this, out var exception))
        {
            throw exception;
        }
    }

    public static IrcAddress FromNickname(ByteStringView nickname)
    {
        if(!TryFromNickname(nickname, out var address, out var exception))
        {
            throw exception;
        }
        return address;
    }

    public static bool TryFromNickname(ByteStringView nickname, out IrcAddress address)
    {
        return TryFromNickname(nickname, out address, out _);
    }

    private static bool TryFromNickname(ByteStringView nickname, out IrcAddress address, [NotNullWhen(false)] out Exception? exception)
    {
        var span = nickname.AsSpan();
        if(span.IsEmpty)
        {
            address = default;
            exception = new ArgumentException("Nickname is empty.", nameof(nickname));
            return false;
        }
        if(span.IndexOfAny(" ,*?!@."u8) != -1)
        {
            address = default;
            exception = new ArgumentException("Nickname contains invalid characters.", nameof(nickname));
            return false;
        }
        if("$:#&+%~"u8.IndexOf(span[0]) != -1)
        {
            address = default;
            exception = new ArgumentException("Nickname starts with an invalid character.", nameof(nickname));
            return false;
        }
        var length = span.Length;
        address = new(nickname, length, length);
        exception = null;
        return true;
    }

    public static IrcAddress FromHost(ByteStringView host)
    {
        if(!TryFromHost(host, out var address, out var exception))
        {
            throw exception;
        }
        return address;
    }

    public static bool TryFromHost(ByteStringView host, out IrcAddress address)
    {
        return TryFromHost(host, out address, out _);
    }

    private static bool TryFromHost(ByteStringView host, out IrcAddress address, [NotNullWhen(false)] out Exception? exception)
    {
        var span = host.AsSpan();
        if(span.IsEmpty)
        {
            address = default;
            exception = new ArgumentException("Host is empty.", nameof(host));
            return false;
        }
        if(span.IndexOfAny("!@"u8) != -1)
        {
            address = default;
            exception = new ArgumentException("Host contains unsupported characters.", nameof(host));
            return false;
        }
        if(span.IndexOf((byte)'.') == -1)
        {
            address = default;
            exception = new ArgumentException("Host must contain the '.' character.", nameof(host));
            return false;
        }
        address = new(host, 0, 0);
        exception = null;
        return true;
    }

    public static bool TryCreate(ReadOnlySpan<byte> nickname, ReadOnlySpan<byte> user, ReadOnlySpan<byte> host, out IrcAddress address)
    {
        return TryCreate(nickname, user, host, out address, out _);
    }

    private static bool TryCreate(ReadOnlySpan<byte> nickname, ReadOnlySpan<byte> user, ReadOnlySpan<byte> host, out IrcAddress address, [NotNullWhen(false)] out Exception? exception)
    {
        if(nickname.IndexOfAny(user.IsEmpty && host.IsEmpty ? ".!@"u8 : "!@"u8) != -1)
        {
            address = default;
            exception = new ArgumentException("Nickname contains unsupported characters.", nameof(nickname));
            return false;
        }
        if(user.IndexOf((byte)'@') != -1)
        {
            address = default;
            exception = new ArgumentException("User contains unsupported characters.", nameof(user));
            return false;
        }
        byte[] buffer;
        int userStart, hostStart;
        if(user.IsEmpty)
        {
            if(nickname.IsEmpty)
            {
                // Bare host
                buffer = host.ToArray();
                userStart = 0;
                hostStart = 0;
            }
            else if(host.IsEmpty)
            {
                // Bare nickname
                buffer = nickname.ToArray();
                userStart = hostStart = nickname.Length;
            }
            else
            {
                // nickname@host
                int hostAt = nickname.Length + 1;
                buffer = new byte[hostAt + host.Length];
                nickname.CopyTo(buffer);
                buffer[nickname.Length] = (byte)'@';
                host.CopyTo(buffer.AsSpan(hostAt));
                userStart = hostStart = hostAt;
            }
        }
        else
        {
            if(nickname.IsEmpty)
            {
                address = default;
                exception = new ArgumentException("Nickname is missing.", nameof(nickname));
                return false;
            }
            if(host.IsEmpty)
            {
                // nickname!user
                int userAt = nickname.Length + 1;
                buffer = new byte[userAt + user.Length];
                nickname.CopyTo(buffer);
                buffer[nickname.Length] = (byte)'!';
                user.CopyTo(buffer.AsSpan(userAt));
                userStart = userAt;
                hostStart = buffer.Length;
            }
            else
            {
                // nickname!user@host
                int userAt = nickname.Length + 1;
                int hostAt = userAt + user.Length + 1;
                buffer = new byte[hostAt + host.Length];
                nickname.CopyTo(buffer);
                buffer[nickname.Length] = (byte)'!';
                user.CopyTo(buffer.AsSpan(userAt));
                buffer[userAt + user.Length] = (byte)'@';
                host.CopyTo(buffer.AsSpan(hostAt));
                userStart = userAt;
                hostStart = hostAt;
            }
        }
        address = new(buffer, userStart, hostStart);
        exception = null;
        return true;
    }

    public static bool TryParse(ByteStringView value, Components components, out IrcAddress address)
    {
        return TryParse(value, components, out address, out _);
    }

    private static bool TryParse(ByteStringView value, Components components, out IrcAddress address, [NotNullWhen(false)] out Exception? exception)
    {
        var span = value.AsSpan();
        int userAt = span.IndexOf((byte)'!');
        int userStart, hostStart;
        if(userAt == -1)
        {
            // No user portion

            int hostAt = span.IndexOf((byte)'@');
            if(hostAt == -1)
            {
                // No separator at all
                if(span.IndexOf((byte)'.') == -1)
                {
                    // Bare nickname
                    exception = NicknameAllowed();
                    if(exception != null)
                    {
                        address = default;
                        return false;
                    }

                    hostStart = span.Length;
                }
                else
                {
                    // Bare servername
                    exception = HostAllowed();
                    if(exception != null)
                    {
                        address = default;
                        return false;
                    }

                    hostStart = 0;
                }
            }
            else
            {
                exception = NicknameAllowed() ?? HostAllowed();
                if(exception != null)
                {
                    address = default;
                    return false;
                }

                hostStart = hostAt + 1;
            }
            userStart = hostStart;
        }
        else
        {
            exception = UserAllowed() ?? NicknameAllowed();
            if(exception != null)
            {
                address = default;
                return false;
            }

            userStart = userAt + 1;

            int hostAt = span.Slice(userStart).IndexOf((byte)'@');
            if(hostAt == -1)
            {
                // No server portion
                hostStart = span.Length;
            }
            else
            {
                exception = HostAllowed();
                if(exception != null)
                {
                    address = default;
                    return false;
                }

                hostStart = userStart + hostAt + 1;
            }
        }
        address = new(value, userStart, hostStart);
        exception = null;
        return true;

        Exception? NicknameAllowed()
        {
            if((components & Components.Nickname) == 0)
            {
                return new ArgumentException("The nickname portion is not allowed.", nameof(value));
            }
            return null;
        }
        Exception? UserAllowed()
        {
            if((components & Components.User) == 0)
            {
                return new ArgumentException("The user portion is not allowed.", nameof(value));
            }
            return null;
        }
        Exception? HostAllowed()
        {
            if((components & Components.Host) == 0)
            {
                return new ArgumentException("The host portion is not allowed.", nameof(value));
            }
            return null;
        }
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public string ToString(Encoding encoding)
    {
        return Value.ToString(encoding);
    }

    public bool Equals(IrcAddress other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is IrcAddress other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(IrcAddress other)
    {
        return Value.CompareTo(other.Value);
    }

    [Flags]
    public enum Components
    {
        None = 0,
        Nickname = 1,
        User = 2,
        Host = 4
    }
}
