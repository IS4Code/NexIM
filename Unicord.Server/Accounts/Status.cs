using Unicord.Primitives;

namespace Unicord.Server.Accounts;

public record struct Status(
    Availability Availability,
    LocalizedString Description = default
);

public enum Availability
{
    Unavailable,
    Available,
    Chatting,
    Away,
    Gone,
    Busy
}
