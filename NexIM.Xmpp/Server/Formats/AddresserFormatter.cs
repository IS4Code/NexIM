using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;

namespace NexIM.Xmpp.Server.Formats;

internal static class AddressesFormatter
{
    public static async ValueTask<IAddressesHandler?> WriteTo(this DeliveryAddress address, IDeliveryHandler deliveryHandler, IAddressesHandler? handler, XmppResource? remoteAddress, bool delivered)
    {
        if(!CanWriteTo(address, remoteAddress, out var type, out var recipient))
        {
            return null;
        }

        if(handler == null)
        {
            handler = await deliveryHandler.Addresses();
        }
        await handler.Address(
            type.ToToken(),
            recipient,
            null,
            null,
            address.Description,
            delivered ? new True() : null
        );
        return handler;
    }

    private static bool CanWriteTo(this DeliveryAddress address, XmppResource? remoteAddress, out AddressType type, out XmppResource? recipient)
    {
        if(address.Type.ToAddressType() is not { } addressType)
        {
            // A type of address that cannot be represented.
            type = default;
            recipient = default;
            return false;
        }
        type = addressType;
        recipient = address.Recipient?.ToResource();
        if(type == AddressType.BlindCarbonCopy && recipient != remoteAddress)
        {
            // Hidden recipients must be ignored
            return false;
        }
        return true;
    }
}
