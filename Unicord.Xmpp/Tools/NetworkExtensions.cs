using System.Net;

namespace Unicord.Xmpp.Tools;

public static class NetworkExtensions
{
    public static bool SameAddressAs(this EndPoint? local, EndPoint? remote)
    {
        return
            local is IPEndPoint { Address: var localAddress } &&
            remote is IPEndPoint { Address: var remoteAddress } &&
            localAddress.Equals(remoteAddress);
    }
}
