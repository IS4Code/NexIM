using Unicord.Server.Primitives;

namespace Unicord.Server.Model;

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
