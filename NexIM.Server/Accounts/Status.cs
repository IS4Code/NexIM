using NexIM.Primitives;

namespace NexIM.Server.Accounts;

public record struct Status(
    Availability Availability,
    LocalizedString? Description = null
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
