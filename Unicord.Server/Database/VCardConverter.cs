using MessagePack;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Unicord.Server.Accounts.VCards;

namespace Unicord.Server.Database;

internal sealed class VCardConverter : ValueConverter<VCard?, byte[]?>
{
    public VCardConverter(MessagePackSerializerOptions options) : base(
        x => Save(x, options),
        x => Load(x, options)
    )
    {
    }

    private static byte[]? Save(VCard? value, MessagePackSerializerOptions options)
    {
        if(value == null)
        {
            return null;
        }
        return MessagePackSerializer.Serialize(value, options);
    }

    private static VCard? Load(byte[]? value, MessagePackSerializerOptions options)
    {
        if(value == null)
        {
            return null;
        }
        return MessagePackSerializer.Deserialize<VCard>(value, options);
    }
}
