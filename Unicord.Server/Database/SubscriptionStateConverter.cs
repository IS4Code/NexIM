using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexIM.Server.Accounts;

namespace NexIM.Server.Database;

internal sealed class SubscriptionStateConverter : ValueConverter<SubscriptionState, ushort>
{
    public SubscriptionStateConverter() : base(
        x => Save(x),
        x => Load(x)
    )
    {
    }

    private static ushort Save(SubscriptionState value)
    {
        return unchecked((ushort)((byte)value.From | ((byte)value.To << 8)));
    }

    private static SubscriptionState Load(ushort value)
    {
        return new((SubscriptionLevel)(value & 0xFF), (SubscriptionLevel)(value >> 8));
    }
}
