namespace Unicord.Server.Model;

public record struct Status(
    Availability Availability,
    string? Description = null
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
