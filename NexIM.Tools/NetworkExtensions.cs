using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NexIM.Tools;

public static class NetworkExtensions
{
    public static bool SameAddressAs(this EndPoint? local, EndPoint? remote)
    {
        return
            local is IPEndPoint { Address: var localAddress } &&
            remote is IPEndPoint { Address: var remoteAddress } &&
            localAddress.Equals(remoteAddress);
    }

    public static string? GetHostName(this EndPoint? endPoint)
    {
        return endPoint switch {
            DnsEndPoint { Host: var host } => host,
            IPEndPoint { Address: var address } => address.GetHostName(),
            _ => null
        };
    }

    public static string GetHostName(this IPAddress address)
    {
        // TODO Resolve using DNS

        if(address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();

        if(address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return $"{bytes[3]}.{bytes[2]}.{bytes[1]}.{bytes[0]}.in-addr.arpa";
        }

        var builder = new StringBuilder(72);
        for(int i = bytes.Length - 1; i >= 0; i--)
        {
            const string hexDigits = "0123456789abcdef";
            builder.Append(hexDigits[bytes[i] & 0x0F]);
            builder.Append('.');
            builder.Append(hexDigits[bytes[i] >> 4]);
            builder.Append('.');
        }
        builder.Append("ip6.arpa");
        return builder.ToString();
    }
}
