using System.Net.Mail;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NexIM.Server.Database;

internal sealed class MailAddressConverter : ValueConverter<MailAddress?, string?>
{
    public MailAddressConverter() : base(
        x => Save(x),
        x => Load(x)
    )
    {
    }

    private static string? Save(MailAddress? value)
    {
        return value?.Address;
    }

    private static MailAddress? Load(string? value)
    {
        return value != null ? new(value) : null;
    }
}
