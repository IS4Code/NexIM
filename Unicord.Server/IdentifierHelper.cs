using System;

namespace Unicord.Server;

internal static class IdentifierHelper
{
    public static DateTimeOffset IdentifierTimeNow => DateTimeOffset.UtcNow;

    public static Guid CreateGuid(DateTimeOffset created)
    {
        return Guid.CreateVersion7(created);
    }
}
