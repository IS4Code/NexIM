using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexIM.Primitives;

namespace NexIM.Server.Database;

internal sealed class LanguageCodeConverter : ValueConverter<LanguageCode?, string?>
{
    public LanguageCodeConverter() : base(
        x => Save(x),
        x => Load(x)
    )
    {
    }

    private static string? Save(LanguageCode? value)
    {
        return value?.Value;
    }

    private static LanguageCode? Load(string? value)
    {
        return value != null ? new(value) : null;
    }
}
