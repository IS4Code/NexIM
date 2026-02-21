namespace Unicord.Server.Model;

public record Contact(
    AccountName Account,
    string? Name = null,
    string? Group = null,
    SubscriptionState SubscriptionState = SubscriptionState.None
);

public enum SubscriptionState
{
    None,
    To,
    From,
    Both
}
